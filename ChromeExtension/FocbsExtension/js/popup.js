
const { div, h1, h4, button, span, br, a, p, h6 } = van.tags;

const loading = van.state(false);
const result = van.state(null);
const error = van.state("");

async function classifyProduct(html, url) {
  const apiBaseUrl = "https://localhost:7444";
  const fullRequestUrl = `${apiBaseUrl}/api/Product/classification/html`;
  try {
    const response = await fetch(fullRequestUrl, {
      method: "POST",
      body: JSON.stringify({ 
        html: html,
        url: url 
      }),
      headers: {
        "Content-Type": "application/json",
      }
    });
    if (response.ok) {
      const jsonString = await response.text();
      const resultModel = JSON.parse(jsonString);
      return resultModel;
    } else {
      const errorContent = await response.text();
      const errorMessage = `Error calling API: ${response.status}. Content: ${errorContent}`;
      return { error: errorMessage };
    }
  } catch (err) {
    return { error: err.toString() };
  }
}

async function handleClassification(html, url) {
  loading.val = true;
  result.val = null;
  error.val = "";
  const classification = await classifyProduct(html, url);
  loading.val = false;
  if (classification.error) {
    error.val = classification.error;
    return;
  }
  result.val = classification;
}

function ResultView() {
  return () => {
    if (error.val) {
      return div({ class: "illegal" }, p(error.val));
    }
    if (!result.val) return "";
    if (result.val.isLegal !== undefined) {
      let state = result.val.isLegal ? "legal" : "illegal";
      if(result.val.productLegality != null) {
        if(result.val.productLegality < 0.333) state = "illegal";
        else if(result.val.productLegality < 0.666) state = "uncertain";
        else state = "legal";
      } 
      return div({ class: state },
        h6(`Product is ${state == "uncertain" ? "a potential risk" : state} (legality score: ${result.val.productLegality})`),
        p(result.val.legalExplanation),
        result.val.linkToLegalDocuments ? [...new Set(result.val.linkToLegalDocuments)]
          .map((href, idx) => p(a({ href: href, target: "_blank" }, "Link to legal document " +  (idx + 1)))) : ""
      );
    } else {
      return div(p(JSON.stringify(result.val)));
    }
  };
}

function LoaderView() {
  return () => loading.val ? span({ id: "loader", "aria-busy": "true" }, "Analyzing the product details") : "";
}

function App() {
  return div({ class: "container" },
    h1("Don't walk straight into the trap"),
    h4("To check wheter the product on the current page is legal and can be imported to switzerland, please click the button below."),
    button({
      id: "sendHtml", disabled: loading, onclick: async () => {
        loading.val = true;
        result.val = null;
        error.val = "";
        const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
        chrome.scripting.executeScript(
          {
            target: { tabId: tab.id },
            func: () => ({ url: document.location.href, html: document.documentElement.outerHTML }),
          },
          (results) => {
            const html = results[0].result.html;
            const url = results[0].result.url;
            handleClassification(html, url);
          }
        );
      }
    }, "Check product"),
    LoaderView(),
    br(),
    br(),
    div({ id: "result" }, ResultView())
  );
}

document.addEventListener("DOMContentLoaded", function () {
  van.add(document.body, App());
});