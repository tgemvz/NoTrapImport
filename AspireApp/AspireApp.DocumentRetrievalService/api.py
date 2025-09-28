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
from nltk.corpus import stopwords

nltk.download("stopwords")

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
    for lang in languages:
        stops = set(stopwords.words(lang))
        words = [w for w in words if w not in stops]
    return words

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
                print(chunk)
                doc = {
                    "docno": f"{base_docno}_{idx}",
                    "filename": filename,
                    "url": "https://" + base_docno.replace("-", "/").replace("fedlex/data/admin/ch/", "www.fedlex.admin.ch/").replace("/html", ""),
                    "text": chunk
                }
                doc_list.append(doc)

    print(f"Prepared {len(doc_list)} document chunks for indexing.")

    if not doc_list:
        print("No documents to index.")
        return

    os.makedirs(INDEX_PATH, exist_ok=True)
    df = pd.DataFrame(doc_list)

    indexer = pt.IterDictIndexer(INDEX_PATH, meta={'docno' : 200, 'filename': 200, 'url': 200, 'text': 30000}, fields=['text'])
    index_ref = indexer.index(df.to_dict(orient='records'))
    retriever = pt.terrier.Retriever(index_ref)
    retriever.controls["wmodel"] = "BM25"
    retriever.metadata = ["docno", "filename", "url", "text"]
    print("Indexing completed.")

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

if __name__ == "__main__":
    if os.path.exists(INDEX_PATH) and os.path.exists(os.path.join(INDEX_PATH, "data.properties")):
        # Load existing index
        index_ref = pt.IndexRef.of(INDEX_PATH)
        retriever = pt.terrier.Retriever(index_ref)
        retriever.controls["wmodel"] = "BM25"
        retriever.metadata = ["docno", "filename", "url", "text"]
        print("Index loaded from disk.")
    else:
        print("Index not found. Creating new index...")
        initialize_index()

    app.run(host='0.0.0.0', port=8001, debug=True)
