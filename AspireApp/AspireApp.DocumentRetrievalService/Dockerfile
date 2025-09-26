FROM python:3.11-slim

RUN apt update
RUN apt install default-jdk -y
RUN pip install flask python-terrier pandas
RUN pip install flask-restx

WORKDIR /app

COPY ./docs /app/data/docs
COPY api.py .

EXPOSE 8001

CMD ["python", "api.py"]
