FROM python:3.11-slim

WORKDIR /app

COPY embedding_api.py .

RUN pip install flask scikit-learn

EXPOSE 8001

CMD ["python", "embedding_api.py"]
