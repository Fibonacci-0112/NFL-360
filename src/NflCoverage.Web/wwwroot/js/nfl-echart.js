// <nfl-echart options='{...}'> renders an Apache ECharts chart from a JSON option object.
// A custom element works the same in static SSR, after enhanced navigation and in interactive render modes:
// the browser upgrades it whenever it is inserted, and re-renders it when the options attribute changes.
const ECHARTS_URL = "https://cdn.jsdelivr.net/npm/echarts@5.5.1/dist/echarts.esm.min.js";
let echartsModule;
const loadECharts = () => (echartsModule ??= import(ECHARTS_URL));

// JSON cannot carry functions, so a few well-known formatter names are mapped here.
const formatters = {
  percent: (value) => (value == null ? "–" : `${value}%`),
};

function reviveFormatters(node) {
  if (Array.isArray(node)) {
    node.forEach(reviveFormatters);
  } else if (node && typeof node === "object") {
    for (const [key, value] of Object.entries(node)) {
      if (key.endsWith("ormatter") && typeof value === "string" && formatters[value]) {
        node[key] = formatters[value];
      } else {
        reviveFormatters(value);
      }
    }
  }
  return node;
}

class NflEChart extends HTMLElement {
  static observedAttributes = ["options"];
  #chart;
  #resizeObserver;
  #theme = window.matchMedia("(prefers-color-scheme: dark)");

  async connectedCallback() {
    const echarts = await loadECharts();
    if (!this.isConnected || this.#chart) return;
    this.#chart = echarts.init(this, this.#theme.matches ? "dark" : undefined, { renderer: "svg" });
    this.#resizeObserver = new ResizeObserver(() => this.#chart?.resize());
    this.#resizeObserver.observe(this);
    this.#render();
  }

  disconnectedCallback() {
    this.#resizeObserver?.disconnect();
    this.#chart?.dispose();
    this.#chart = undefined;
  }

  attributeChangedCallback() {
    this.#render();
  }

  #render() {
    if (!this.#chart) return;
    try {
      const options = reviveFormatters(JSON.parse(this.getAttribute("options") ?? "{}"));
      this.#chart.setOption({ backgroundColor: "transparent", ...options }, true);
    } catch (error) {
      console.error("nfl-echart: invalid options", error);
    }
  }
}

customElements.define("nfl-echart", NflEChart);
