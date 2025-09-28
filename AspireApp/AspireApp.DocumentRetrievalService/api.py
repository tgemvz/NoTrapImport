# -*- coding: utf-8 -*-
from webbrowser import get
from flask import Flask, request, jsonify
from flask_restx import Api, Resource, fields
import pyterrier as pt
import pandas as pd
import os
import glob
import json
import re
import nltk
from nltk.corpus import stopwords, wordnet
from nltk.stem import SnowballStemmer, WordNetLemmatizer
from sentence_transformers import SentenceTransformer
from sklearn.metrics.pairwise import cosine_similarity
import numpy as np
import shutil


nltk.download("stopwords")
nltk.download('wordnet')
nltk.download('omw-1.4')

# Ensure required NLTK resources are available
def safe_nltk_download(resource):
    try:
        nltk.data.find(resource)
    except LookupError:
        nltk.download(resource.split('/')[-1])

safe_nltk_download('corpora/stopwords')
safe_nltk_download('corpora/wordnet')
safe_nltk_download('corpora/omw-1.4')

# Initialize PyTerrier
if not pt.java.started():
    pt.java.init()

# Flask app and API
app = Flask(__name__)
api = Api(app, version='1.0', title='Document Search API',
          description='A simple BM25-based document search API with PyTerrier and Swagger UI')

ns = api.namespace('search', description='Search operations')

# Models
query_model = api.model('Query', {
    'query': fields.String(required=True, description='Search query'),
    'k': fields.Integer(description='Number of results to return', default=5)
})

# Constants
DOCUMENTS_DIR = "/app/data/docs"
INDEX_PATH = "/app/data/index"
MAX_WORDS_PER_CHUNK = 300

# Globals
retriever = None

# Semantische Suche: Modell und Embeddings
SEMANTIC_MODEL_NAME = "paraphrase-multilingual-MiniLM-L12-v2"
semantic_model = SentenceTransformer(SEMANTIC_MODEL_NAME)
semantic_embeddings = None
semantic_doc_chunks = None

def split_text_into_chunks(words, max_words, overlap_words):
    """Splits text into chunks of at most max_words words with overlap."""
    chunks = []
    i = 0
    while i < len(words):
        chunk = " ".join(words[i:i+max_words])
        chunks.append(chunk)
        i += max_words - overlap_words
    return chunks

def clean_pipeline(text):
    languages = ["english", "german", "french", "italian"]
    words = text.lower().split()
    
    # Remove stopwords for all languages
    for lang in languages:
        try:
            stops = set(stopwords.words(lang))
            words = [w for w in words if w not in stops]
        except OSError:
            continue  # skip if stopwords not available for that language

    # Apply stemming using SnowballStemmer
    stemmed_words = []
    for lang in languages:
        try:
            stemmer = SnowballStemmer(lang)
            stemmed_words = [stemmer.stem(w) for w in words]
            break  # Use first matching language (you might customize this)
        except ValueError:
            continue  # language not supported by SnowballStemmer

    # Apply lemmatization (English only by default)
    lemmatizer = WordNetLemmatizer()
    lemmatized_words = [lemmatizer.lemmatize(w) for w in stemmed_words]

    return lemmatized_words

def filename_to_url(filename: str) -> str:
    # Step 1: Remove the fixed prefix
    prefix = "fedlex-data-admin-ch-eli-cc-"
    if not filename.startswith(prefix):
        raise ValueError("Filename does not start with expected prefix.")
    trimmed = filename[len(prefix):]

    # Step 2: Split at '-' to extract year and id
    parts = trimmed.split('-')
    if len(parts) < 4:
        raise ValueError("Filename does not contain enough parts.")
    
    year = parts[0]
    id_ = parts[1]


    html_index = parts.index("html")
    lang = parts[html_index - 1]

    # Step 4: Construct and return the URL
    return f"https://www.fedlex.admin.ch/eli/cc/{year}/{id_}/{lang}"

def initialize_index():
    global retriever
    doc_list = []

    print("Loading markdown documents...")
    if not os.path.exists(DOCUMENTS_DIR):
        print(f"Documents directory '{DOCUMENTS_DIR}' does not exist.")
        return

    # Get all .md files
    doc_files = glob.glob(os.path.join(DOCUMENTS_DIR, "*.md"))
    print(f"Found {len(doc_files)} markdown files.")

    for filepath in doc_files:
        with open(filepath, "r", encoding="utf-8") as f:
            content = f.read()
            content = clean_pipeline(content)
            filename = os.path.basename(filepath)
            base_docno = os.path.splitext(filename)[0]

            # Split content into chunks
            chunks = split_text_into_chunks(content, MAX_WORDS_PER_CHUNK, int(MAX_WORDS_PER_CHUNK / 2))
            for idx, chunk in enumerate(chunks):
                doc = {
                    "docno": f"{base_docno}_{idx}",
                    "filename": filename,
                    "url": filename_to_url(base_docno),
                    "text": chunk
                }
                doc_list.append(doc)

    print(f"Prepared {len(doc_list)} document chunks for indexing.")

    if not doc_list:
        print("No documents to index.")
        return

    os.makedirs(INDEX_PATH, exist_ok=True)
    df = pd.DataFrame(doc_list)

    if os.path.exists(INDEX_PATH):
        print(f"Index already exists at {INDEX_PATH}. Deleting the existing index.")
        shutil.rmtree(INDEX_PATH)

    indexer = pt.IterDictIndexer(INDEX_PATH, meta={'docno' : 200, 'filename': 200, 'url': 200, 'text': 30000}, fields=['text'])
    index_ref = indexer.index(df.to_dict(orient='records'))
    retriever = pt.terrier.Retriever(index_ref)
    retriever.controls["wmodel"] = "BM25"
    retriever.metadata = ["docno", "filename", "url", "text"]
    print("Indexing completed.")

    # Semantische Suche: Embeddings initialisieren
    initialize_semantic_embeddings(doc_list)

    # Nach dem Erstellen:
    np.save("/app/data/semantic_embeddings.npy", semantic_embeddings)
    with open("/app/data/semantic_doc_chunks.json", "w", encoding="utf-8") as f:
        json.dump(semantic_doc_chunks, f, ensure_ascii=False)

def initialize_semantic_embeddings(doc_list):
    global semantic_embeddings, semantic_doc_chunks
    texts = [doc["text"] for doc in doc_list]
    semantic_doc_chunks = doc_list
    semantic_embeddings = semantic_model.encode(texts, show_progress_bar=True, convert_to_numpy=True)

def sanitize_query(query):
    return re.sub(r'[()\[\]{}^~*?:\"\\]', '', query)
    
@ns.route('/query')
class QueryDocuments(Resource):
    @ns.expect(query_model)
    def post(self):
        """Query the indexed documents"""
        global retriever

        if retriever is None:
            return {"error": "Index is not initialized."}, 500

        data = request.json
        query_text = data.get("query", "")
        k = int(data.get("k", 5))

        if not query_text:
            return {"error": "Query is required"}, 400

        query_text = sanitize_query(query_text)
        query_text = " ".join(clean_pipeline(query_text))

        print("query with ", query_text)

        query_df = pd.DataFrame([{"qid": "1", "query": query_text}])
        results = retriever.transform(query_df).head(k)

        response = []
        for _, row in results.iterrows():
            response.append({
                "docno": row.get("docno"),
                "score": row.get("score"),
                "url": row.get("url"),
                "text":  row.get("text")
            })

        return jsonify(response)

@ns.route('/semantic_query')
class SemanticQueryDocuments(Resource):
    @ns.expect(query_model)
    def post(self):
        """Query the indexed documents using semantic search"""
        data = request.json
        query_text = data.get("query", "")
        k = int(data.get("k", 5))

        if not query_text:
            return {"error": "Query is required"}, 400

        query_text = sanitize_query(query_text)
        query_text = " ".join(clean_pipeline(query_text))

        print("semantic query with ", query_text)

        results = semantic_search(query_text, k)

        response = []
        for row in results:
            response.append({
                "docno": row.get("docno"),
                "score": row.get("score"),
                "url": row.get("url"),
                "text":  row.get("text")
            })

        return jsonify(response)

def semantic_search(query, k=5):
    if semantic_embeddings is None:
        return []
    query_emb = semantic_model.encode([query], convert_to_numpy=True)
    sims = cosine_similarity(query_emb, semantic_embeddings)[0]
    top_idx = np.argsort(sims)[::-1][:k]
    results = []
    for idx in top_idx:
        doc = semantic_doc_chunks[idx]
        results.append({
            "docno": doc["docno"],
            "score": float(sims[idx]),
            "url": doc["url"],
            "text": doc["text"]
        })
    return results

if __name__ == "__main__":
    # 1. Prüfe, ob Embeddings und Chunks existieren
    embeddings_path = "/app/data/semantic_embeddings.npy"
    chunks_path = "/app/data/semantic_doc_chunks.json"

    if os.path.exists(INDEX_PATH) and os.path.exists(os.path.join(INDEX_PATH, "data.properties")):
        # Index laden
        index_ref = pt.IndexRef.of(INDEX_PATH)
        retriever = pt.terrier.Retriever(index_ref)
        retriever.controls["wmodel"] = "BM25"
        retriever.metadata = ["docno", "filename", "url", "text"]
        print("Index loaded from disk.")

        # 2. Embeddings und Chunks laden, falls vorhanden
        if os.path.exists(embeddings_path) and os.path.exists(chunks_path):
            semantic_embeddings = np.load(embeddings_path)
            with open(chunks_path, "r", encoding="utf-8") as f:
                semantic_doc_chunks = json.load(f)
        else:
            # Dokumente laden und Embeddings neu berechnen
            doc_list = []
            doc_files = glob.glob(os.path.join(DOCUMENTS_DIR, "*.md"))
            for filepath in doc_files:
                with open(filepath, "r", encoding="utf-8") as f:
                    content = f.read()
                    content = clean_pipeline(content)
                    filename = os.path.basename(filepath)
                    base_docno = os.path.splitext(filename)[0]
                    chunks = split_text_into_chunks(content, MAX_WORDS_PER_CHUNK, int(MAX_WORDS_PER_CHUNK / 2))
                    for idx, chunk in enumerate(chunks):
                        doc = {
                            "docno": f"{base_docno}_{idx}",
                            "filename": filename,
                            "url": "https://" + base_docno.replace("-", "/").replace("fedlex/data/admin/ch/", "www.fedlex.admin.ch/").replace("/html", ""),
                            "text": chunk
                        }
                        doc_list.append(doc)
            if doc_list:
                initialize_semantic_embeddings(doc_list)
                np.save(embeddings_path, semantic_embeddings)
                with open(chunks_path, "w", encoding="utf-8") as f:
                    json.dump(semantic_doc_chunks, f, ensure_ascii=False)
    else:
        print("Index wird neu erstellt, da kein Index vorhanden ist.")
        initialize_index()
        # Das Speichern der Embeddings passiert bereits in initialize_index()

    app.run(host='0.0.0.0', port=8001, debug=True)
