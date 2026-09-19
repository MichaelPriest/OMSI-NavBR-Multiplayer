import React, { useEffect, useMemo, useState } from "react";
import { AdSlot, Footer, Header, MonetizationScripts, ScrollProgress } from "./SiteChrome.jsx";
import { Hero, StatusSection, TrustStrip } from "./HeroSections.jsx";
import { AllVersions, AlphaDownloads, Downloads, Features } from "./DownloadSections.jsx";
import { DocumentationSection, MultiplayerSection, SupportSection } from "./ProjectSections.jsx";
import DownloadDrawer from "./DownloadDrawer.jsx";
import ConceptGallery from "./ConceptGallery.jsx";
import { useActiveSection, useReleaseCatalog } from "./hooks.js";
import { CURRENT_TAG, RELEASES_PAGE, alphaKey } from "./lib.js";

export default function App() {
  const catalog = useReleaseCatalog();
  const [downloadsOpen, setDownloadsOpen] = useState(false);
  const sectionIds = ["inicio", "estado", "download", "todas-versoes", "downloads-por-alpha", "recursos", "multiplayer", "documentacao", "contribua"];
  const activeSection = useActiveSection(sectionIds);

  const current = useMemo(
    () => catalog.releases.find(release => release?.tag_name === CURRENT_TAG) || null,
    [catalog.releases]
  );
  const currentAlphaKey = alphaKey(CURRENT_TAG);
  const currentAlphaDownloads = Number(catalog.alphaDownloads?.[currentAlphaKey]) || 0;
  const currentAssets = (current?.assets || []).filter(asset => /\.(exe|zip)$/i.test(asset.name || ""));
  const standalone = currentAssets.find(asset => /win-x86\.exe$/i.test(asset.name || ""));

  useEffect(() => {
    document.body.classList.add("navbr-support-enabled");
    return () => document.body.classList.remove("navbr-support-enabled");
  }, []);

  return (
    <>
      <ScrollProgress />
      <MonetizationScripts />
      <Header activeSection={activeSection} onOpenDownloads={() => setDownloadsOpen(true)} />
      <DownloadDrawer
        open={downloadsOpen}
        assets={currentAssets}
        loading={catalog.loading}
        error={catalog.error}
        releasesPage={RELEASES_PAGE}
        onClose={() => setDownloadsOpen(false)}
      />
      <main>
        <SupportSection />
        <Hero current={current} standalone={standalone} />
        <TrustStrip current={current} alphaDownloads={currentAlphaDownloads} totalDownloads={catalog.totalDownloads} />
        <AdSlot name="top" />
        <StatusSection current={current} />
        <ConceptGallery />
        <Downloads currentAssets={currentAssets} current={current} loading={catalog.loading} error={catalog.error} releasesPage={RELEASES_PAGE} />
        <AllVersions releases={catalog.releases} loading={catalog.loading} error={catalog.error} releasesPage={RELEASES_PAGE} />
        <AlphaDownloads currentAlphaKey={currentAlphaKey} alphaDownloads={catalog.alphaDownloads} loading={catalog.loading} />
        <Features />
        <AdSlot name="direct" />
        <MultiplayerSection />
        <AdSlot name="content" />
        <DocumentationSection releases={catalog.releases} />
      </main>
      <Footer />
    </>
  );
}
