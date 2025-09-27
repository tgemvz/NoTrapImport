---
theme: default
paginate: false
---

<!-- footer: ![w:150](logo.svg) -->

<style>
footer {
  /* Unset default placing inherited from the built-in theme */
  left: auto;
  right: auto;
  top: auto;
  bottom: auto;

  /* Place to right-bottom */
  right: 20px;
  bottom: 20px;
}
</style>

# **Don't fall into the trap (Gov3)**

Stay safe and compliant while online shopping

---

<style>
img[alt~="center"] {
  display: block;
  margin: 0 auto;
}
</style>
![h:600 center](Copilot_20250926_181137.png)

<!-- Excitedly waiting for your new Blaster Master 4000! Only to find customs to reject the package. Worst case, they're even sending you a letter with legal actions. -->

---

![h:500 center](../Doc/example_usage.png)

--- 

![h:600 center](sequence.drawio.svg)

<!-- 
- That's were we come in. 
- Input 
  - as easy as possible: WebExtension vs WebApp
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
-->

---
<style scoped>
section{
  font-size:10px; 
}
</style>
![h:500 center](illegal_packages.png)

<center>
  <a href="https://www.srf.ch/news/schweiz/onlinehandel-besuch-beim-zoll-wie-sich-kunden-verbotene-waren-liefern-lassen">SRF: Besuch beim Zoll: Wie sich Kunden verbotene Waren liefern lassen</a>
</center>

<!--
- over 600 products in one week! (679)
-->

---

![h:500 center](../Doc/order_process_checked.png)

<center>

# Q & A

</center>