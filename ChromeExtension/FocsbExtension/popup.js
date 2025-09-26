async function classifyProduct(html, url) {
  const apiBaseUrl = "https://localhost:7444";
  const fullRequestUrl = `${apiBaseUrl}/api/Product/classification/html`;

  try {
    const response = await fetch(fullRequestUrl, {
      method: "POST",
      body: JSON.stringify({ html: html }),
      headers: {
        "Content-Type": "application/json",
      }
    }).catch((err) => {
      return err;
    })

    if (response.ok) {
      const jsonString = await response.text();
      const resultModel = JSON.parse(jsonString);
      return resultModel;
    } else {
      const errorContent = await response.text();
      const errorMessage = `Error calling API: ${response.status}. Content: ${errorContent}`;
      return errorMessage;
    }
  } catch (err) {
    return err;
  }
}

async function handleClassification(html, url) {
  setLoading(true)
  const classification = await classifyProduct(html);
  document.getElementById("result").innerText = classification;
  setLoading(false)
}

function setLoading(set) {
  const htmlElement = document.getElementById("loader");
  const button = document.getElementById("sendHtml");
  if (set) {
    htmlElement.classList.remove('hidden');
    button.disabled = true;
  } else {
    htmlElement.classList.add('hidden');
    button.disabled = false;
  }
}

async function init() {
  document.getElementById("sendHtml").addEventListener("click", async () => {
    // classifyProduct("<html><body>Test</body></html>", "sdf")
    // return

    const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
    chrome.scripting.executeScript(
      {
        target: { tabId: tab.id },
        func: () => ({ url: document.location.href, html: document.documentElement.outerText }),
      },
      (results) => {

        const html = results[0].result.html
        const url = results[0].result.url;
        handleClassification(html, url);
      }
    );
  });
}

document.addEventListener("DOMContentLoaded", function () {
  init();
});