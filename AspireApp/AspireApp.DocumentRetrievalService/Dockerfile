FROM python:3.11-slim

RUN apt update

RUN pip install flask
RUN pip install flask-restx
RUN pip install nltk spacy
RUN pip install sentence-transformers scikit-learn

RUN python -m nltk.downloader stopwords
RUN python -m spacy download en_core_web_sm

WORKDIR /app

COPY ./docs /app/data/docs
COPY api.py .

EXPOSE 8001

CMD ["python", "api.py"]
