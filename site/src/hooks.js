import { useEffect, useState } from "react";
import { calculateAllAlphaDownloads, releaseDownloadCount } from "./lib.js";

export function useReleaseCatalog() {
  const [state, setState] = useState({
    loading: true,
    releases: [],
    totalDownloads: 0,
    alphaDownloads: {},
    error: null
  });

  useEffect(() => {
    let active = true;
    fetch("./releases.json", { cache: "no-store" })
      .then(response => {
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        return response.json();
      })
      .then(catalog => {
        if (!active) return;
        const releases = Array.isArray(catalog)
          ? catalog
          : Array.isArray(catalog?.releases) ? catalog.releases : [];
        // releases.json follows the GitHub Releases ordering. Preserve it so
        // numbered test builds remain in their intended sequence even when a
        // release was edited or published later than a higher-numbered build.
        const totalDownloads = Number(catalog?.total_downloads) ||
          releases.reduce((total, release) => total + releaseDownloadCount(release), 0);
        const alphaDownloads = catalog?.alpha_downloads && typeof catalog.alpha_downloads === "object"
          ? catalog.alpha_downloads
          : calculateAllAlphaDownloads(releases);
        setState({ loading: false, releases, totalDownloads, alphaDownloads, error: null });
      })
      .catch(error => {
        if (active) {
          setState({ loading: false, releases: [], totalDownloads: 0, alphaDownloads: {}, error: error.message });
        }
      });
    return () => { active = false; };
  }, []);

  return state;
}

export function useActiveSection(ids) {
  const [active, setActive] = useState(ids[0] || "inicio");

  useEffect(() => {
    if (!("IntersectionObserver" in window)) return undefined;
    const nodes = ids.map(id => document.getElementById(id)).filter(Boolean);
    const observer = new IntersectionObserver(entries => {
      const visible = entries
        .filter(entry => entry.isIntersecting)
        .sort((a, b) => b.intersectionRatio - a.intersectionRatio)[0];
      if (visible?.target?.id) setActive(visible.target.id);
    }, { threshold: [0.15, 0.35, 0.6], rootMargin: "-18% 0px -62% 0px" });
    nodes.forEach(node => observer.observe(node));
    return () => observer.disconnect();
  }, [ids.join("|")]);

  return active;
}

export function useScrollProgress() {
  const [width, setWidth] = useState(0);

  useEffect(() => {
    const update = () => {
      const scrollable = document.documentElement.scrollHeight - window.innerHeight;
      const ratio = scrollable > 0 ? window.scrollY / scrollable : 0;
      setWidth(Math.max(0, Math.min(1, ratio)) * 100);
    };
    update();
    window.addEventListener("scroll", update, { passive: true });
    window.addEventListener("resize", update);
    return () => {
      window.removeEventListener("scroll", update);
      window.removeEventListener("resize", update);
    };
  }, []);

  return width;
}
