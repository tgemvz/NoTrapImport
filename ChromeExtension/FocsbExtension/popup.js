document.getElementById("sendHtml").addEventListener("click", async () => {
  const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
  chrome.scripting.executeScript(
    {
      target: { tabId: tab.id },
      func: () => document.documentElement.outerHTML,
    },
    (results) => {
      const html = results[0].result;
      fetch("http://localhost:5055/receive-html", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ html }),
      });
    }
  );
});
