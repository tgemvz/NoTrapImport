FROM python:3.11-slim

RUN apt update
RUN apt install default-jdk -y
RUN pip install flask python-terrier pandas
RUN pip install flask-restx
RUN pip install nltk spacy
RUN pip install sentence-transformers scikit-learn
RUN python -m nltk.downloader stopwords punkt
RUN python -m spacy download en_core_web_sm
RUN python3 -c "import pyterrier as pt; pt.init()"

WORKDIR /app

COPY ./docs /app/data/docs
COPY api.py .

EXPOSE 8001

CMD ["python", "api.py"]
