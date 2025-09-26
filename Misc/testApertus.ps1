curl -X POST https://api.swisscom.com/layer/swiss-ai-weeks/apertus-70b/v1/chat/completions -H "Authorization: Bearer $env:SWISS_AI_PLATFORM_API_KEY" -H "Content-Type: application/json" -d '{
  "model": "swiss-ai/Apertus-70B",
  "messages": [
    {
      "role": "system",
      "content": "You are a helpful math tutor. Break down your reasoning into clear steps, and provide a final answer. Respond with JSON only that exactly matches the schema: \"response_format\": { \"type\": \"json_schema\", \"json_schema\": { \"name\": \"math_response\", \"strict\": true, \"schema\": { \"type\": \"object\", \"properties\": { \"steps\": { \"type\": \"array\", \"items\": { \"type\": \"object\", \"properties\": { \"explanation\": { \"type\": \"string\" }, \"output\": { \"type\": \"string\" } }, \"required\": [\"explanation\", \"output\"], \"additionalProperties\": false } }, \"final_answer\": { \"type\": \"string\" } }, \"required\": [\"steps\", \"final_answer\"], \"additionalProperties\": false } } } Do not include any text outside of the JSON object."
    },
    {
      "role": "user",
      "content": "solve 8x + 31 = 2"
    }
  ], 
  "max_tokens": 4096,
  "temperature": 0.0,
  "top_p": 1.0
}'
