import type { CapacitorConfig } from "@capacitor/cli";

const config: CapacitorConfig = {
  appId: "br.navbr.mobile",
  appName: "NavBR Mobile",
  webDir: "dist",
  server: {
    androidScheme: "http"
  },
  android: {
    allowMixedContent: true
  }
};

export default config;
