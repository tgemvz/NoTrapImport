- 1 min, 3min QA
- 2 min, 1 min QA

# Criteria

## Jury
- Reproducible code is preferred
- Idea, concept and execution takes AI paradigm into account
- If responding to a challenges, the objectives mentioned there are addressed

## Challenge
- Based on EAN > no free API, thus extracting data ourselves
- Based on pulicly available information
- ToS/Usage Policies not violated
- Evaluation by ease of use and classification performance

## Presentation

- Structure
  1. Hook: Problem/Use Case (Curious, pain, vision)
  > Excitedly waiting for your new Blaster Master 4000! Only to find customs to reject the package. Worst case, they're even sending you a letter with legal actions.
  1. Solution + Demo
      1. Show, don't tell
      1. How becomes what (use analogy)
  1. Market Impact
      1. what does it lead to
      1. Address the challenge
  1. Team 
      1. why can they rely on us?
      1. WHat are the next step

- Layers
  - Input 
    - as easy as possible: WebExtension
    - Sanitation (HTML, strip down)
  - API
    - currently URLs
    - extendable
  - Extraction
    - Use LLM with sanitized input
    - Get structure output: product description, category, product identification number
  - Legality Check
    - (Curated) FEDLEX catalogue as vectors (Swiss Legal Guidelines)
    - Query relevant documents
    - RAG approach: Use LLM to provide confidence regarding product legality in context of the retrieved documents