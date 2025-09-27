from flask import Flask, request, jsonify
from sklearn.feature_extraction.text import TfidfVectorizer
import numpy as np

app = Flask(__name__)

@app.route("/test", methods=["GET"])
def test():
    return jsonify({"text": "hello world"}), 200

@app.route("/embed", methods=["POST"])
def embed_text():
    data = request.get_json()
    if not data or "texts" not in data:
        return jsonify({"error": "Missing 'texts' in request"}), 400

    texts = data["texts"]
    if not isinstance(texts, list):
        return jsonify({"error": "'texts' must be a list of strings"}), 400

    vectorizer = TfidfVectorizer()
    vectors = vectorizer.fit_transform(texts).toarray()
    vectors_list = vectors.tolist()
    return jsonify({"vectors": vectors_list})


if __name__ == "__main__":
    app.run(host="0.0.0.0", port=8001)
