import type { SVGProps } from "react";

export type NavBrIconName =
  | "home" | "navigation" | "multiplayer" | "roleplay" | "ghost"
  | "operations" | "company" | "hardware" | "settings" | "help"
  | "refresh" | "play" | "bus" | "roomAdd" | "roomJoin" | "server"
  | "users" | "map" | "chat" | "microphone" | "volume" | "network"
  | "firewall" | "plugin" | "download" | "logs" | "route"
  | "destination" | "door" | "light" | "turnLeft" | "turnRight"
  | "hazard" | "brake" | "reverse" | "engine" | "test" | "info"
  | "clipboardCopy" | "clipboardPaste" | "star" | "record" | "stop"
  | "character" | "action" | "straight" | "slightLeft" | "slightRight"
  | "sharpLeft" | "sharpRight" | "rejoin";

type Props = SVGProps<SVGSVGElement> & {
  name: NavBrIconName;
  size?: number;
};

export function NavBrIcon({ name, size = 20, className, ...props }: Props) {
  const common = {
    viewBox: "0 0 24 24",
    width: size,
    height: size,
    fill: "none",
    stroke: "currentColor",
    strokeWidth: 1.8,
    strokeLinecap: "round" as const,
    strokeLinejoin: "round" as const,
    "aria-hidden": true
  };

  const glyph = (() => {
    switch (name) {
      case "home": return <><path d="M3 11.2 12 4l9 7.2"/><path d="M5.5 10.5V20h13v-9.5"/><path d="M9.2 20v-5.7h5.6V20"/></>;
      case "navigation": return <><circle cx="12" cy="12" r="8.5"/><path d="m15.8 7.5-2.1 6.2-6.2 2.1 2.1-6.2 6.2-2.1Z"/><circle cx="11.7" cy="11.7" r="1"/></>;
      case "multiplayer": return <><path d="M4 16.5V9.2l2.4-2.4h11.2L20 9.2v7.3"/><path d="M7 16.5h10"/><circle cx="8" cy="11.5" r="1.4"/><circle cx="16" cy="11.5" r="1.4"/><path d="M6 18.5h12"/></>;
      case "roleplay": return <><circle cx="12" cy="6.5" r="2.5"/><path d="M8.2 20v-5.6c0-2.1 1.7-3.8 3.8-3.8s3.8 1.7 3.8 3.8V20"/><path d="m8.2 15-2.5 2.2M15.8 15l2.5 2.2"/></>;
      case "ghost": return <><path d="M6 19V10a6 6 0 0 1 12 0v9l-2-1.5-2 1.5-2-1.5-2 1.5-2-1.5L6 19Z"/><circle cx="10" cy="11" r=".8" fill="currentColor" stroke="none"/><circle cx="14" cy="11" r=".8" fill="currentColor" stroke="none"/></>;
      case "operations": return <><rect x="4" y="4" width="16" height="16" rx="2"/><path d="M8 15V9m4 8V7m4 6v-3"/><path d="M7 15h2m2 2h2m2-4h2"/></>;
      case "company": return <><path d="M4 20V8l8-4 8 4v12"/><path d="M8 20v-4h8v4M8 10h2m4 0h2M8 13h2m4 0h2"/></>;
      case "hardware": return <><rect x="5" y="4" width="14" height="16" rx="2"/><path d="M9 8h6M8 12h8M9 16h2m2 0h2"/><path d="M12 2v2M12 20v2"/></>;
      case "settings": return <><circle cx="12" cy="12" r="3"/><path d="M19 12a7 7 0 0 0-.1-1.2l2-1.5-2-3.4-2.4 1a7 7 0 0 0-2-1.2L14.2 3h-4.4l-.3 2.7a7 7 0 0 0-2 1.2l-2.4-1-2 3.4 2 1.5A7 7 0 0 0 5 12c0 .4 0 .8.1 1.2l-2 1.5 2 3.4 2.4-1a7 7 0 0 0 2 1.2l.3 2.7h4.4l.3-2.7a7 7 0 0 0 2-1.2l2.4 1 2-3.4-2-1.5c.1-.4.1-.8.1-1.2Z"/></>;
      case "help": return <><circle cx="12" cy="12" r="9"/><path d="M9.8 9a2.4 2.4 0 1 1 3.7 2c-1 .6-1.5 1.1-1.5 2.2"/><path d="M12 17h.01"/></>;
      case "refresh": return <><path d="M20 7v5h-5"/><path d="M18.4 9A7 7 0 1 0 19 15"/></>;
      case "play": return <><path d="M8 5.5 18 12 8 18.5v-13Z"/></>;
      case "bus": return <><rect x="5" y="3" width="14" height="17" rx="3"/><path d="M7.5 6h9v6h-9zM5 14h14"/><circle cx="8" cy="18" r="1"/><circle cx="16" cy="18" r="1"/></>;
      case "roomAdd": return <><path d="M4 20V7l8-3 8 3v13"/><path d="M12 4v16M15 11h4m-2-2v4"/></>;
      case "roomJoin": return <><path d="M4 20V7l8-3 8 3v13"/><path d="M8 12h8m-3-3 3 3-3 3"/></>;
      case "server": return <><rect x="4" y="4" width="16" height="6" rx="1.5"/><rect x="4" y="14" width="16" height="6" rx="1.5"/><circle cx="8" cy="7" r=".8" fill="currentColor" stroke="none"/><circle cx="8" cy="17" r=".8" fill="currentColor" stroke="none"/><path d="M12 7h5M12 17h5"/></>;
      case "users": return <><circle cx="9" cy="9" r="3"/><path d="M3.5 20c.4-3.3 2.5-5 5.5-5s5.1 1.7 5.5 5"/><circle cx="17" cy="8" r="2"/><path d="M15.5 14.5c3.2-.2 4.8 1.5 5 4"/></>;
      case "map": return <><path d="m4 6 5-2 6 2 5-2v14l-5 2-6-2-5 2V6Z"/><path d="M9 4v14m6-12v14"/></>;
      case "chat": return <><path d="M4 5h16v11H9l-5 4V5Z"/><path d="M8 9h8M8 12h5"/></>;
      case "microphone": return <><rect x="9" y="3" width="6" height="11" rx="3"/><path d="M6 11a6 6 0 0 0 12 0M12 17v4m-3 0h6"/></>;
      case "volume": return <><path d="M4 10h4l4-4v12l-4-4H4v-4Z"/><path d="M16 9a4 4 0 0 1 0 6m2-8a7 7 0 0 1 0 10"/></>;
      case "network": return <><circle cx="12" cy="5" r="2"/><circle cx="5" cy="18" r="2"/><circle cx="19" cy="18" r="2"/><path d="M12 7v4M6.7 16.5 10.5 12h3l3.8 4.5"/></>;
      case "firewall": return <><path d="M12 3 19 6v5c0 4.5-2.7 7.6-7 10-4.3-2.4-7-5.5-7-10V6l7-3Z"/><path d="M8 9h8M8 13h8M10 9v4m4-4v4"/></>;
      case "plugin": return <><path d="M9 4v4H5v7h4v5h6v-5h4V8h-4V4h-6Z"/><path d="M11 8V6m2 2V6"/></>;
      case "download": return <><path d="M12 4v11m-4-4 4 4 4-4"/><path d="M5 20h14"/></>;
      case "logs": return <><path d="M6 3h9l3 3v15H6V3Z"/><path d="M15 3v4h4M9 11h6m-6 3h6m-6 3h4"/></>;
      case "route": return <><circle cx="6" cy="18" r="2"/><circle cx="18" cy="6" r="2"/><path d="M8 18h3a3 3 0 0 0 3-3V9a3 3 0 0 1 3-3"/></>;
      case "destination": return <><path d="M12 21s6-5.4 6-11a6 6 0 1 0-12 0c0 5.6 6 11 6 11Z"/><circle cx="12" cy="10" r="2"/></>;
      case "door": return <><path d="M6 21V4h12v17M9 21V7h6v14"/><circle cx="13.2" cy="14" r=".6" fill="currentColor" stroke="none"/></>;
      case "light": return <><path d="M5 9c3-4 6-4 9 0v6c-3 4-6 4-9 0V9Z"/><path d="M16 8h4m-4 4h5m-5 4h4"/></>;
      case "turnLeft": return <><path d="M10 6 4 12l6 6"/><path d="M5 12h7c4 0 7 2 7 6"/></>;
      case "turnRight": return <><path d="m14 6 6 6-6 6"/><path d="M19 12h-7c-4 0-7 2-7 6"/></>;
      case "hazard": return <><path d="m12 4 9 16H3L12 4Z"/><path d="M12 9v5m0 3h.01"/></>;
      case "brake": return <><circle cx="12" cy="12" r="6"/><path d="M5 6.5C3.7 8 3 9.8 3 12s.7 4 2 5.5M19 6.5c1.3 1.5 2 3.3 2 5.5s-.7 4-2 5.5"/><path d="M10 9h3a2 2 0 0 1 0 4h-3v3"/></>;
      case "reverse": return <><path d="M7 18V6h6a4 4 0 0 1 0 8H7"/><path d="m14 14 4 4m0 0v-4m0 4h-4"/></>;
      case "engine": return <><path d="M5 9h3l2-3h5l2 3h2v8h-3l-1.5 2h-6L7 17H5V9Z"/><path d="M10 11h4v4h-4z"/></>;
      case "test": return <><path d="M9 3h6v4l4 11a2 2 0 0 1-1.9 3H6.9A2 2 0 0 1 5 18L9 7V3Z"/><path d="M8 15h8"/></>;
      case "info": return <><circle cx="12" cy="12" r="9"/><path d="M12 10v7m0-10h.01"/></>;
      case "clipboardCopy": return <><rect x="8" y="7" width="11" height="13" rx="2"/><path d="M15 7V5a2 2 0 0 0-2-2H7a2 2 0 0 0-2 2v10a2 2 0 0 0 2 2h1"/><path d="M11 11h5m-5 3h5m-5 3h3"/></>;
      case "clipboardPaste": return <><path d="M9 5h6"/><path d="M10 3h4a2 2 0 0 1 2 2v1H8V5a2 2 0 0 1 2-2Z"/><rect x="5" y="5" width="14" height="16" rx="2"/><path d="M12 10v7m-3-3 3 3 3-3"/></>;
      case "star": return <path d="m12 3 2.7 5.5 6.1.9-4.4 4.3 1 6.1-5.4-2.9-5.4 2.9 1-6.1-4.4-4.3 6.1-.9L12 3Z"/>;
      case "record": return <circle cx="12" cy="12" r="6.5" fill="currentColor" stroke="none"/>;
      case "stop": return <rect x="6" y="6" width="12" height="12" rx="1.8" fill="currentColor" stroke="none"/>;
      case "character": return <><circle cx="12" cy="6.3" r="2.4"/><path d="M8.5 20v-5.5c0-2.1 1.6-3.8 3.5-3.8s3.5 1.7 3.5 3.8V20"/><path d="M8.5 14.5 6 17m9.5-2.5L18 17M10.2 20l-.8 2m4.4-2 .8 2"/></>;
      case "action": return <><path d="M13.5 2.8 7.8 12h4l-1.3 9.2 5.7-9.2h-4l1.3-9.2Z"/><path d="M4 6h3m10 12h3"/></>;
      case "straight": return <><path d="M12 20V5"/><path d="m7.5 9.5 4.5-4.5 4.5 4.5"/></>;
      case "slightLeft": return <><path d="M15.5 20v-5.8c0-2.1-.8-3.7-2.5-5L8 5.5"/><path d="M8 10V5.5h4.5"/></>;
      case "slightRight": return <><path d="M8.5 20v-5.8c0-2.1.8-3.7 2.5-5l5-3.7"/><path d="M16 10V5.5h-4.5"/></>;
      case "sharpLeft": return <><path d="M17 20v-8H8"/><path d="m11.5 8.5-3.5 3.5 3.5 3.5"/></>;
      case "sharpRight": return <><path d="M7 20v-8h9"/><path d="m12.5 8.5 3.5 3.5-3.5 3.5"/></>;
      case "rejoin": return <><path d="M18 8a7 7 0 1 0 1 8"/><path d="M18 3v5h-5"/><path d="M12 17v-5m0 0 3 3m-3-3-3 3"/></>;
    }
  })();

  return <svg className={["navbr-icon", className].filter(Boolean).join(" ")} {...common} {...props}>{glyph}</svg>;
}
