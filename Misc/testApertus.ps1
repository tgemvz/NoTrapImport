curl -X POST https://api.swisscom.com/layer/swiss-ai-weeks/apertus-70b/v1/chat/completions -H "Authorization: Bearer $env:SWISS_AI_PLATFORM_API_KEY" -H "Content-Type: application/json" -d '{
  "model": "swiss-ai/Apertus-70B",
  "messages": [
    {"role": "user", "content": "Hello, how are you?"}
  ]
		}'