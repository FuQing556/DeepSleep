FROM python:3.12-slim
WORKDIR /app
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt && useradd --uid 10001 --create-home relay
COPY relay.py .
USER relay
ENV BIND=0.0.0.0 PORT=8765
EXPOSE 8765
CMD ["python", "relay.py"]
