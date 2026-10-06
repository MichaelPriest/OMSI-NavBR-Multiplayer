param(
    [Parameter(Mandatory = $true)]
    [string]$OpenOmsiSource,
    [ValidateSet("stock", "mesh")]
    [string]$WorldGuidanceRenderer = "stock"
)

$ErrorActionPreference = "Stop"

function Replace-Required {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Old,
        [Parameter(Mandatory = $true)][string]$New
    )
    $text = Get-Content -LiteralPath $Path -Raw
    if (-not $text.Contains($Old)) {
        throw "Required openOMSI source anchor not found in $Path"
    }
    $text = $text.Replace($Old, $New)
    Set-Content -LiteralPath $Path -Value $text -Encoding UTF8 -NoNewline
}

$plugin = Join-Path $OpenOmsiSource "crates/omsi-plugin/src/lib.rs"
$appLib = Join-Path $OpenOmsiSource "crates/omsi-app/src/lib.rs"
$app = Join-Path $OpenOmsiSource "crates/omsi-app/src/app.rs"
$events = Join-Path $OpenOmsiSource "crates/omsi-app/src/app_events.rs"
$inputScript = Join-Path $OpenOmsiSource "crates/omsi-app/src/input_script.rs"
$navigator = Join-Path $OpenOmsiSource "crates/omsi-app/src/navigator.rs"
$offscreen = Join-Path $OpenOmsiSource "crates/omsi-app/src/offscreen.rs"
$ui = Join-Path $OpenOmsiSource "crates/omsi-app/src/ui.rs"

# --- omsi-plugin: optional in-process renderer export.
Replace-Required $plugin @'
type AccessTriggerFn = unsafe extern "system" fn(u16, *mut u8);
'@ @'
type AccessTriggerFn = unsafe extern "system" fn(u16, *mut u8);
type OverlayFrameFn = unsafe extern "system" fn(*mut u8, i32) -> i32;
type HudFlagsFn = unsafe extern "system" fn(u32, u32);
'@

Replace-Required $plugin @'
    string: Option<AccessStringFn>,
}
'@ @'
    string: Option<AccessStringFn>,
    overlay_frame_v2: Option<OverlayFrameFn>,
    hud_flags_v1: Option<HudFlagsFn>,
}
'@

Replace-Required $plugin @'
            let string = lib.get::<AccessStringFn>(b"AccessStringVariable\0").ok().map(|s| *s);
            Ok(Library { _lib: lib, start, finalize, variable, trigger, system, string })
'@ @'
            let string = lib.get::<AccessStringFn>(b"AccessStringVariable\0").ok().map(|s| *s);
            let overlay_frame_v2 = lib
                .get::<OverlayFrameFn>(b"OpenOmsiGetOverlayFrameV2\0")
                .ok()
                .map(|s| *s);
            let hud_flags_v1 = lib
                .get::<HudFlagsFn>(b"OpenOmsiSetHudFlagsV1\0")
                .ok()
                .map(|s| *s);
            Ok(Library { _lib: lib, start, finalize, variable, trigger, system, string, overlay_frame_v2, hud_flags_v1 })
'@

Replace-Required $plugin @'
    pub fn procs(&self) -> Procs {
        Procs { variable: self.variable.is_some(), trigger: self.trigger.is_some(), system: self.system.is_some(), string: self.string.is_some() }
    }

    /// `PluginStart(AOwner)`; there is no Delphi application to own the plugin's forms here.
'@ @'
    pub fn procs(&self) -> Procs {
        Procs { variable: self.variable.is_some(), trigger: self.trigger.is_some(), system: self.system.is_some(), string: self.string.is_some() }
    }

    pub fn set_hud_flags_v1(&self, mask: u32, values: u32) -> bool {
        let Some(set) = self.hud_flags_v1 else { return false };
        unsafe { set(mask, values) };
        true
    }

    pub fn overlay_frame_v2(&self) -> Option<Vec<u8>> {
        let read = self.overlay_frame_v2?;
        let mut required = unsafe { read(std::ptr::null_mut(), 0) };
        if !(2..=262_144).contains(&required) {
            return None;
        }
        let mut bytes = vec![0u8; required as usize];
        let mut written = unsafe { read(bytes.as_mut_ptr(), required) };
        if written >= required {
            required = written;
            if !(2..=262_144).contains(&required) {
                return None;
            }
            bytes.resize(required as usize, 0);
            written = unsafe { read(bytes.as_mut_ptr(), required) };
        }
        if written <= 0 || written >= required {
            return None;
        }
        bytes.truncate(written as usize);
        Some(bytes)
    }

    /// `PluginStart(AOwner)`; there is no Delphi application to own the plugin's forms here.
'@

Replace-Required $plugin @'
    pub fn procs(&self) -> Procs {
        self.procs
    }

    /// Run one frame. `system` / `var` / `string` read a value by name (None: no such
'@ @'
    pub fn procs(&self) -> Procs {
        self.procs
    }

    pub fn overlay_frame_v2(&self) -> Option<Vec<u8>> {
        match &self.backend {
            Backend::Local(lib) => lib.overlay_frame_v2(),
            Backend::Remote(_) => None,
        }
    }

    pub fn set_hud_flags_v1(&self, mask: u32, values: u32) -> bool {
        match &self.backend {
            Backend::Local(lib) => lib.set_hud_flags_v1(mask, values),
            Backend::Remote(_) => false,
        }
    }

    /// Run one frame. `system` / `var` / `string` read a value by name (None: no such
'@

Replace-Required $plugin @'
    pub fn frame(&mut self, io: &mut dyn PluginIo) {
        for p in &mut self.loaded {
            p.frame(io);
        }
        for p in &mut self.lua {
            p.frame(io);
        }
    }

    pub fn finalize(&mut self) {
'@ @'
    pub fn frame(&mut self, io: &mut dyn PluginIo) {
        for p in &mut self.loaded {
            p.frame(io);
        }
        for p in &mut self.lua {
            p.frame(io);
        }
    }

    pub fn overlay_frames_v2(&self) -> Vec<Vec<u8>> {
        self.loaded.iter().filter_map(Plugin::overlay_frame_v2).collect()
    }

    pub fn set_navbr_hud_flag(&self, bit: u32, enabled: bool) -> bool {
        let mask = 1u32 << bit;
        let values = if enabled { mask } else { 0 };
        self.loaded
            .iter()
            .any(|plugin| plugin.set_hud_flags_v1(mask, values))
    }

    pub fn finalize(&mut self) {
'@

# --- omsi-app module/state wiring.
Replace-Required $appLib @'
mod player;
mod plugins;
mod services;
'@ @'
mod player;
mod plugins;
mod navbr_overlay;
mod navbr_world_guidance;
mod services;
'@

Replace-Required $appLib @'
        route_arrows: Default::default(),
        game_keys:
'@ @'
        route_arrows: Default::default(),
        navbr_world_guidance: Default::default(),
        game_keys:
'@

Replace-Required $appLib @'
        log_state: Default::default(),
        plugins: None,
        career:
'@ @'
        log_state: Default::default(),
        plugins: None,
        navbr_overlay: Default::default(),
        navbr_panel_open: false,
        career:
'@

Replace-Required $app @'
    pub(crate) route_arrows: crate::route_arrows::RouteArrows,
    /// OMSI's global key actions
'@ @'
    pub(crate) route_arrows: crate::route_arrows::RouteArrows,
    pub(crate) navbr_world_guidance: crate::navbr_world_guidance::WorldGuidance,
    /// OMSI's global key actions
'@

Replace-Required $app @'
    pub(crate) plugins: Option<omsi_plugin::Plugins>,
    /// The on-screen controls
'@ @'
    pub(crate) plugins: Option<omsi_plugin::Plugins>,
    pub(crate) navbr_overlay: crate::navbr_overlay::State,
    pub(crate) navbr_panel_open: bool,
    /// The on-screen controls
'@

# --- consume the cached v2 export after normal plugin frame work.
Replace-Required $events @'
                    plugins.frame(&mut io);
                    let commands = std::mem::take(&mut io.commands);
                    if let Some(m) = io.message {
                        self.service_msg = Some(m);
                    }
'@ @'
                    plugins.frame(&mut io);
                    let overlay_frames = plugins.overlay_frames_v2();
                    let commands = std::mem::take(&mut io.commands);
                    let message = io.message.take();
                    drop(io);
                    if let Some(m) = message {
                        self.service_msg = Some(m);
                    }
                    self.navbr_overlay.update(overlay_frames);
'@

# --- NavBR in-game panel shortcut: Ctrl+Alt+N; Escape closes it first.
Replace-Required $inputScript @'
            if pressed && !repeat {
                self.keys.insert(code);
            } else if !pressed {
                self.keys.remove(&code);
            }
            // Alt+Enter: full screen on and off
'@ @'
            if pressed && !repeat {
                self.keys.insert(code);
            } else if !pressed {
                self.keys.remove(&code);
            }

            let navbr_ctrl = self.keys.contains(&KeyCode::ControlLeft)
                || self.keys.contains(&KeyCode::ControlRight);
            let navbr_alt = self.keys.contains(&KeyCode::AltLeft)
                || self.keys.contains(&KeyCode::AltRight);
            if pressed && !repeat && code == KeyCode::KeyN && navbr_ctrl && navbr_alt {
                self.navbr_panel_open = !self.navbr_panel_open;
                self.service_msg = Some((
                    if self.navbr_panel_open {
                        "NavBR: painel aberto"
                    } else {
                        "NavBR: painel fechado"
                    }.to_string(),
                    2.0,
                ));
                return;
            }
            if pressed && !repeat && code == KeyCode::Escape && self.navbr_panel_open {
                self.navbr_panel_open = false;
                return;
            }

            // Alt+Enter: full screen on and off
'@

# --- optional NavBR camera hints for the existing navigator.
Replace-Required $navigator @'
    pub follow_window: bool,
    pub dt: f32,
'@ @'
    pub follow_window: bool,
    pub navbr_radius_meters: Option<f64>,
    pub navbr_orientation_mode: Option<&'a str>,
    pub dt: f32,
'@

Replace-Required $events @'
                            follow_window: if vr_active {
                                true
                            } else {
                                self.settings.ui_scale_window
                            },
                            dt,
'@ @'
                            follow_window: if vr_active {
                                true
                            } else {
                                self.settings.ui_scale_window
                            },
                            navbr_radius_meters: self.navbr_overlay.overlay_2d.as_ref().map(|n| n.radius_meters),
                            navbr_orientation_mode: self.navbr_overlay.overlay_2d.as_ref().map(|n| n.orientation_mode.as_str()),
                            dt,
'@

Replace-Required $offscreen @'
                follow_window: settings.ui_scale_window,
                dt: 0.1,
'@ @'
                follow_window: settings.ui_scale_window,
                navbr_radius_meters: None,
                navbr_orientation_mode: None,
                dt: 0.1,
'@

Replace-Required $navigator @'
        let want = (110.0 + f.speed_kmh as f64 * 2.2).clamp(110.0, 280.0);
        if self.first {
            self.zoom = want;
            self.cam_heading = f.heading;
        }
        self.zoom += (want - self.zoom) * ease(f.dt, 1.8);
        self.cam_heading += angle_diff(self.cam_heading, f.heading) * ease(f.dt, 0.3);
'@ @'
        let want = f
            .navbr_radius_meters
            .filter(|v| v.is_finite())
            .map(|v| v.clamp(110.0, 2400.0))
            .unwrap_or_else(|| (110.0 + f.speed_kmh as f64 * 2.2).clamp(110.0, 280.0));
        let wanted_heading = match f.navbr_orientation_mode {
            Some("north-up") => 0.0,
            _ => f.heading,
        };
        if self.first {
            self.zoom = want;
            self.cam_heading = wanted_heading;
        }
        self.zoom += (want - self.zoom) * ease(f.dt, 1.8);
        self.cam_heading += angle_diff(self.cam_heading, wanted_heading) * ease(f.dt, 0.3);
'@

Replace-Required $navigator @'
        NavFrame { traffic: self.traffic, players: self.players.clone(), bus: self.bus, heading: self.heading, speed_kmh: self.speed_kmh, outside_temp: self.outside_temp, inside_temp: self.inside_temp, line: self.line.clone(), terminus: self.terminus.clone(), stops: self.stops.clone(), delay: self.delay, passengers: self.passengers, time: self.time, weekday: self.weekday, language: self.language, screen: self.screen, ui_scale: self.ui_scale, follow_window: self.follow_window, dt: self.dt, stop_requested: self.stop_requested, info_rect: self.info_rect }
'@ @'
        NavFrame { traffic: self.traffic, players: self.players.clone(), bus: self.bus, heading: self.heading, speed_kmh: self.speed_kmh, outside_temp: self.outside_temp, inside_temp: self.inside_temp, line: self.line.clone(), terminus: self.terminus.clone(), stops: self.stops.clone(), delay: self.delay, passengers: self.passengers, time: self.time, weekday: self.weekday, language: self.language, screen: self.screen, ui_scale: self.ui_scale, follow_window: self.follow_window, navbr_radius_meters: self.navbr_radius_meters, navbr_orientation_mode: self.navbr_orientation_mode, dt: self.dt, stop_requested: self.stop_requested, info_rect: self.info_rect }
'@

# --- apply full-map toggle on transitions only, preserving native Escape/click close.
Replace-Required $events @'
                    if let (Some(nav), Some(p), Some(_)) = (
                        self.navigator.as_mut(),
                        self.player.as_ref(),
                        self.surface.as_ref(),
                    ) {
                        let old_enabled = nav.enabled;
'@ @'
                    let navbr_full_map_change = self.navbr_overlay.take_full_map_change();
                    if let (Some(nav), Some(p), Some(_)) = (
                        self.navigator.as_mut(),
                        self.player.as_ref(),
                        self.surface.as_ref(),
                    ) {
                        if let Some(open) = navbr_full_map_change {
                            nav.city.open = open;
                        }
                        let old_enabled = nav.enabled;
'@

# --- let NavBR drive the existing openOMSI navigator layers for this frame.
Replace-Required $events @'
                        let old_enabled = nav.enabled;
                        let old_opacity = nav.opacity;
                        nav.cockpit_display = vr_active;
                        if vr_active {
                            nav.enabled = vr_nav_display.is_some_and(|d| d.placement.enabled);
                            nav.opacity = vr_nav_display.map(|d| d.placement.opacity).unwrap_or(0.95);
                        }
'@ @'
                        let old_enabled = nav.enabled;
                        let old_opacity = nav.opacity;
                        let old_arrows = nav.arrows;
                        let old_show_ai = nav.show_ai;
                        nav.cockpit_display = vr_active;
                        if vr_active {
                            nav.enabled = vr_nav_display.is_some_and(|d| d.placement.enabled);
                            nav.opacity = vr_nav_display.map(|d| d.placement.opacity).unwrap_or(0.95);
                        } else if let Some(n) = self.navbr_overlay.overlay_2d.as_ref() {
                            nav.enabled = n.mini_map_visible;
                            let navbr_world_visible = self
                                .navbr_overlay
                                .world
                                .as_ref()
                                .is_some_and(|world| world.visible);
                            nav.arrows = n.route_guidance_visible && !navbr_world_visible;
                            nav.show_ai = n.traffic_visible;
                        }
'@

Replace-Required $events @'
                            players: self.lan.as_ref().map(|l| crate::lan::nav_players(&self.remotes, l.my_id)).unwrap_or_default(),
'@ @'
                            players: if self.navbr_overlay.overlay_2d.as_ref().is_none_or(|n| n.players_visible) {
                                self.lan.as_ref().map(|l| crate::lan::nav_players(&self.remotes, l.my_id)).unwrap_or_default()
                            } else {
                                Vec::new()
                            },
'@

Replace-Required $events @'
                        nav.frame_at(r, scene, &frame, hud[0]);
                        nav.enabled = old_enabled;
                        nav.opacity = old_opacity;
'@ @'
                        nav.frame_at(r, scene, &frame, hud[0]);
                        nav.enabled = old_enabled;
                        nav.opacity = old_opacity;
                        nav.arrows = old_arrows;
                        nav.show_ai = old_show_ai;
'@

# --- world-space fallback guidance using native helper objects.
Replace-Required $events @'
                        } else if self.route_arrows.any() {
                            // (switched off in the menu: the ones standing go too)
                            if let Some(w) = self.world.as_ref() {
                                self.route_arrows.clear(w, r, scene);
                            }
                        }
                    }
                    if let (Some(ui), Some(s)) = (self.ui.as_mut(), self.surface.as_ref()) {
'@ @'
                        } else if self.route_arrows.any() {
                            // (switched off in the menu: the ones standing go too)
                            if let Some(w) = self.world.as_ref() {
                                self.route_arrows.clear(w, r, scene);
                            }
                        }
                    }

                    if let (Some(w), Some(frame)) =
                        (self.world.as_ref(), self.navbr_overlay.world.as_ref())
                    {
                        self.navbr_world_guidance.tick(
                            dt,
                            self.started.elapsed().as_secs_f32(),
                            w,
                            r,
                            scene,
                            frame,
                        );
                    } else if self.navbr_world_guidance.any() {
                        if let Some(w) = self.world.as_ref() {
                            self.navbr_world_guidance.clear(w, r, scene);
                        }
                    }

                    if let (Some(ui), Some(s)) = (self.ui.as_mut(), self.surface.as_ref()) {
'@

# --- native 2D UI frame.
Replace-Required $ui @'
    pub notice_anchor: Option<[f32; 4]>,
    /// What kind of menu the lines belong to.
'@ @'
    pub notice_anchor: Option<[f32; 4]>,
    pub navbr_overlay: Option<&'a crate::navbr_overlay::Overlay2D>,
    pub navbr_panel_open: bool,
    /// What kind of menu the lines belong to.
'@

Replace-Required $events @'
                            notices: &self.notices,
                            notice_anchor: self.navigator.as_ref().and_then(|n| n.screen_rect()),
'@ @'
                            notices: &self.notices,
                            notice_anchor: self.navigator.as_ref().and_then(|n| n.screen_rect()),
                            navbr_overlay: self.navbr_overlay.overlay_2d.as_ref(),
                            navbr_panel_open: self.navbr_panel_open,
'@

Replace-Required $ui @'
        if let Some(fps) = f.fps {
'@ @'
        if let Some(nav) = f.navbr_overlay.filter(|n| n.tele_matrix_visible) {
            let s = f.scale.max(0.5) * f.ui_scale;
            let width = (300.0 * s).min(f.width * 0.36);
            let x = 16.0 * s;
            let y = 70.0 * s;
            let pad = 10.0 * s;
            let line = nav.tele_matrix_line.as_deref().unwrap_or("--");
            let destination = nav.tele_matrix_destination.as_deref().unwrap_or("Sem destino");
            let next = nav.tele_matrix_next_stop.as_deref().unwrap_or("Sem próxima parada");
            let delay = nav.tele_matrix_delay_seconds.map(|v| {
                let sign = if v > 0 { "+" } else { "" };
                format!("{sign}{} min", v / 60)
            }).unwrap_or_else(|| "--".into());
            let state = nav.tele_matrix_punctuality_state.as_deref().unwrap_or("unknown");

            let title = self.text.label(r, scene, &format!("Linha {line}  {delay}"), (16.0 * s) as u32, [255, 255, 255, 0]);
            let dest = self.text.label(r, scene, destination, (14.0 * s) as u32, [225, 225, 225, 0]);
            let stop = self.text.label(r, scene, &format!("Próxima: {next}"), (13.0 * s) as u32, [205, 215, 230, 0]);
            let status = self.text.label(r, scene, state, (11.0 * s) as u32, [170, 190, 210, 0]);
            let h = title.h as f32 + dest.h as f32 + stop.h as f32 + status.h as f32 + pad * 2.0 + 10.0 * s;
            let plate = self.text.plate(r, scene, 3);
            scene.overlays.push((plate, [x, y, x + width, y + h]));

            let tx = x + pad;
            let mut ty = y + pad;
            for label in [title, dest, stop, status] {
                scene.overlays.push((label.tex, [tx, ty, tx + label.w as f32, ty + label.h as f32]));
                ty += label.h as f32 + 3.0 * s;
            }
        }

        if let Some(nav) = f.navbr_overlay.filter(|n| n.compact_hud_visible) {
            let s = f.scale.max(0.5) * f.ui_scale;
            let width = (430.0 * s).min(f.width * 0.56);
            let pad = 12.0 * s;
            let x = ((f.width - width) * 0.5).round();
            let y = 58.0 * s;
            let primary = nav.primary_text.as_deref().unwrap_or("Navigation");
            let detail = nav.secondary_text.as_deref().unwrap_or("");

            let distance = nav.distance_to_maneuver_meters.map(|m| {
                if m >= 1000.0 { format!("{:.1} km", m / 1000.0) } else { format!("{:.0} m", m) }
            }).unwrap_or_default();
            let remaining = nav.route_remaining_meters.map(|m| {
                if m >= 1000.0 { format!("{:.1} km restantes", m / 1000.0) } else { format!("{:.0} m restantes", m) }
            }).unwrap_or_default();
            let icon = match nav.maneuver_icon.as_deref() {
                Some("turn-left") => "←",
                Some("turn-right") => "→",
                Some("slight-left") => "↖",
                Some("slight-right") => "↗",
                Some("uturn") => "↶",
                Some("rejoin") => "⤴",
                _ => "↑",
            };

            let title = if distance.is_empty() {
                format!("{icon} {primary}")
            } else {
                format!("{icon} {distance}  {primary}")
            };

            let p = self.text.label(r, scene, &title, (21.0 * s) as u32, [255, 255, 255, 0]);
            let d = self.text.label(r, scene, detail, (14.0 * s) as u32, [220, 220, 220, 0]);
            let rem = self.text.label(r, scene, &remaining, (12.0 * s) as u32, [190, 205, 220, 0]);
            let progress = nav.route_progress_percent.map(|v| (v / 100.0).clamp(0.0, 1.0) as f32);

            let mut h = p.h as f32 + pad * 2.0;
            if !detail.is_empty() { h += d.h as f32 + 4.0 * s; }
            if !remaining.is_empty() { h += rem.h as f32 + 3.0 * s; }
            if progress.is_some() { h += 8.0 * s; }

            let plate = self.text.plate(r, scene, if nav.off_route { 6 } else { 3 });
            scene.overlays.push((plate, [x, y, x + width, y + h]));
            let tx = x + pad;
            let mut ty = y + pad;
            scene.overlays.push((p.tex, [tx, ty, tx + p.w as f32, ty + p.h as f32]));
            ty += p.h as f32 + 4.0 * s;
            if !detail.is_empty() {
                scene.overlays.push((d.tex, [tx, ty, tx + d.w as f32, ty + d.h as f32]));
                ty += d.h as f32 + 3.0 * s;
            }
            if !remaining.is_empty() {
                scene.overlays.push((rem.tex, [tx, ty, tx + rem.w as f32, ty + rem.h as f32]));
                ty += rem.h as f32 + 4.0 * s;
            }
            if let Some(progress) = progress {
                let track_w = width - pad * 2.0;
                let track_h = 4.0 * s;
                let track = self.text.solid(r, scene, [70, 78, 90, 180]);
                let fill = self.text.solid(
                    r,
                    scene,
                    if nav.off_route { [230, 126, 70, 230] } else { [80, 170, 255, 230] },
                );
                scene.overlays.push((track, [tx, ty, tx + track_w, ty + track_h]));
                scene.overlays.push((fill, [tx, ty, tx + track_w * progress, ty + track_h]));
            }
        }

        if let Some(fps) = f.fps {
'@

$overlay = @'
use serde::Deserialize;

#[derive(Debug, Clone, Default)]
pub(crate) struct State {
    timestamp: i64,
    pub(crate) overlay_2d: Option<Overlay2D>,
    pub(crate) world: Option<WorldGuidance>,
    full_map_change: Option<bool>,
}

impl State {
    pub(crate) fn update(&mut self, frames: Vec<Vec<u8>>) {
        let newest = frames
            .into_iter()
            .filter_map(|bytes| serde_json::from_slice::<Envelope>(&bytes).ok())
            .filter(|frame| frame.version == 2)
            .max_by_key(|frame| frame.timestamp_unix_milliseconds);
        let Some(frame) = newest else { return };
        if frame.timestamp_unix_milliseconds <= self.timestamp {
            return;
        }
        let full_map = frame.overlay_2_d.full_map_visible;
        let previous_full_map = self
            .overlay_2d
            .as_ref()
            .is_some_and(|overlay| overlay.full_map_visible);
        if full_map != previous_full_map {
            self.full_map_change = Some(full_map);
        }

        self.timestamp = frame.timestamp_unix_milliseconds;
        self.overlay_2d = Some(frame.overlay_2_d);
        self.world = Some(frame.world_guidance);
    }

    pub(crate) fn take_full_map_change(&mut self) -> Option<bool> {
        self.full_map_change.take()
    }
}

#[derive(Debug, Clone, Deserialize)]
#[serde(rename_all = "PascalCase")]
struct Envelope {
    version: i32,
    timestamp_unix_milliseconds: i64,
    #[serde(rename = "Overlay2D")]
    overlay_2_d: Overlay2D,
    world_guidance: WorldGuidance,
}

#[derive(Debug, Clone, Deserialize)]
#[serde(rename_all = "PascalCase")]
pub(crate) struct Overlay2D {
    pub(crate) mini_map_visible: bool,
    pub(crate) full_map_visible: bool,
    pub(crate) compact_hud_visible: bool,
    pub(crate) tele_matrix_visible: bool,
    pub(crate) route_guidance_visible: bool,
    pub(crate) traffic_visible: bool,
    pub(crate) players_visible: bool,
    pub(crate) auto_zoom_enabled: bool,
    pub(crate) follow_vehicle_enabled: bool,
    pub(crate) timetable_visible: bool,
    pub(crate) congestion_visible: bool,
    pub(crate) radius_meters: f64,
    pub(crate) orientation_mode: String,
    pub(crate) tele_matrix_line: Option<String>,
    pub(crate) tele_matrix_destination: Option<String>,
    pub(crate) tele_matrix_next_stop: Option<String>,
    pub(crate) tele_matrix_delay_seconds: Option<i32>,
    pub(crate) tele_matrix_punctuality_state: Option<String>,
    pub(crate) primary_text: Option<String>,
    pub(crate) secondary_text: Option<String>,
    pub(crate) maneuver_icon: Option<String>,
    pub(crate) distance_to_maneuver_meters: Option<f64>,
    pub(crate) route_progress_percent: Option<f64>,
    pub(crate) route_remaining_meters: Option<f64>,
    pub(crate) off_route: bool,
}

#[derive(Debug, Clone, Deserialize)]
#[serde(rename_all = "PascalCase")]
pub(crate) struct WorldGuidance {
    pub(crate) visible: bool,
    pub(crate) ground_arrow_primitives: Vec<GroundArrowPrimitive>,
}

#[derive(Debug, Clone, Deserialize)]
#[serde(rename_all = "PascalCase")]
pub(crate) struct GroundArrowPrimitive {
    pub(crate) x: f64,
    pub(crate) y: f64,
    pub(crate) z: f64,
    pub(crate) heading_degrees: f64,
    pub(crate) width_meters: f64,
    pub(crate) length_meters: f64,
    pub(crate) height_offset_meters: f64,
    pub(crate) opacity: f64,
    pub(crate) kind: String,
}
'@
Set-Content -LiteralPath (Join-Path $OpenOmsiSource "crates/omsi-app/src/navbr_overlay.rs") -Value $overlay -Encoding UTF8 -NoNewline

$world = @'
use crate::navbr_overlay::WorldGuidance as Frame;
use crate::scene::{TileGpu, World};
use glam::DVec3;
use omsi_render::{Renderer, Scene};
use std::hash::{Hash, Hasher};

#[derive(Default)]
pub(crate) struct WorldGuidance {
    placed: Vec<(u64, TileGpu)>,
    wait: f32,
}

fn sco(kind: &str) -> &'static str {
    match kind {
        "left" | "slight-left" => "Sceneryobjects\\Generic\\routearrow_L_dyn.sco",
        "right" | "slight-right" => "Sceneryobjects\\Generic\\routearrow_R_dyn.sco",
        _ => "Sceneryobjects\\Generic\\routearrow_dn_dyn.sco",
    }
}

fn key(x: f64, y: f64, z: f64, heading: f64, kind: &str) -> u64 {
    let mut h = std::collections::hash_map::DefaultHasher::new();
    ((x * 10.0).round() as i64).hash(&mut h);
    ((y * 10.0).round() as i64).hash(&mut h);
    ((z * 10.0).round() as i64).hash(&mut h);
    ((heading * 10.0).round() as i64).hash(&mut h);
    kind.hash(&mut h);
    h.finish()
}

impl WorldGuidance {
    pub(crate) fn tick(
        &mut self,
        dt: f32,
        _seconds: f32,
        world: &World,
        renderer: &Renderer,
        scene: &mut Scene,
        frame: &Frame,
    ) {
        self.wait -= dt;
        if self.wait > 0.0 { return; }
        self.wait = 0.5;
        if !frame.visible {
            self.clear(world, renderer, scene);
            return;
        }

        let wanted: Vec<u64> = frame
            .ground_arrow_primitives
            .iter()
            .filter(|p| p.opacity > 0.05)
            .take(24)
            .map(|p| key(p.x, p.y, p.z, p.heading_degrees, &p.kind))
            .collect();

        let mut kept = Vec::with_capacity(self.placed.len());
        for (k, object) in self.placed.drain(..) {
            if wanted.contains(&k) {
                kept.push((k, object));
            } else {
                world.remove_helper_object(renderer, scene, object);
            }
        }
        self.placed = kept;

        for p in frame.ground_arrow_primitives.iter().filter(|p| p.opacity > 0.05).take(24) {
            let k = key(p.x, p.y, p.z, p.heading_degrees, &p.kind);
            if self.placed.iter().any(|(existing, _)| *existing == k) {
                continue;
            }
            let z = world
                .walk_height(p.x, p.y)
                .filter(|z| (z - p.z).abs() < 1.5)
                .unwrap_or(p.z);
            if let Some(object) = world.add_helper_object(
                renderer,
                scene,
                sco(&p.kind),
                DVec3::new(p.x, p.y, z),
                p.heading_degrees,
                &[],
            ) {
                self.placed.push((k, object));
            }
        }
    }

    pub(crate) fn any(&self) -> bool {
        !self.placed.is_empty()
    }

    pub(crate) fn clear(&mut self, world: &World, renderer: &Renderer, scene: &mut Scene) {
        for (_, object) in self.placed.drain(..) {
            world.remove_helper_object(renderer, scene, object);
        }
        self.wait = 0.0;
    }
}
'@
if ($WorldGuidanceRenderer -eq "mesh") {
    $world = @'
use crate::navbr_overlay::WorldGuidance as Frame;
use glam::{DVec3, Mat4, Vec2, Vec3};
use omsi_geometry::MeshData;
use omsi_render::{AlphaMode, MaterialId, MeshId, RenderPhase, Renderer, Scene};

#[derive(Default)]
pub(crate) struct WorldGuidance {
    mesh: Option<MeshId>,
    material: Option<MaterialId>,
    instances: Vec<usize>,
    active: usize,
}

fn arrow_mesh() -> MeshData {
    MeshData {
        positions: vec![
            Vec3::new( 0.00,  0.50, 0.0),
            Vec3::new( 0.50,  0.05, 0.0),
            Vec3::new( 0.18,  0.05, 0.0),
            Vec3::new( 0.18, -0.50, 0.0),
            Vec3::new(-0.18, -0.50, 0.0),
            Vec3::new(-0.18,  0.05, 0.0),
        ],
        normals: vec![Vec3::Z; 6],
        uvs: vec![Vec2::ZERO; 6],
        ranges: vec![(0, 12, 0)],
        indices: vec![0, 1, 2, 0, 2, 5, 5, 2, 3, 5, 3, 4],
        one_sided: false,
    }
}

fn transform(heading: f64, width: f64, length: f64) -> Mat4 {
    Mat4::from_rotation_z(-(heading as f32).to_radians())
        * Mat4::from_scale(Vec3::new(width as f32, length as f32, 1.0))
}

impl WorldGuidance {
    fn ensure_assets(&mut self, renderer: &Renderer, scene: &mut Scene) -> (MeshId, MaterialId) {
        let mesh = *self.mesh.get_or_insert_with(|| renderer.add_mesh(scene, &arrow_mesh()));
        let material = *self.material.get_or_insert_with(|| {
            renderer.add_material(scene, None, AlphaMode::Blend, [0.10, 0.72, 1.00, 0.90], true)
        });
        (mesh, material)
    }

    pub(crate) fn tick(
        &mut self,
        _dt: f32,
        seconds: f32,
        world: &crate::scene::World,
        renderer: &Renderer,
        scene: &mut Scene,
        frame: &Frame,
    ) {
        if !frame.visible {
            for &instance in &self.instances {
                renderer.set_params(scene, instance, &[], false, &[]);
            }
            return;
        }

        let wanted: Vec<_> = frame
            .ground_arrow_primitives
            .iter()
            .filter(|p| p.opacity > 0.05)
            .take(24)
            .collect();
        let (mesh, material) = self.ensure_assets(renderer, scene);

        while self.instances.len() < wanted.len() {
            let instance = renderer.add_instance(
                scene,
                mesh,
                DVec3::ZERO,
                Mat4::IDENTITY,
                vec![material],
            );
            scene.instances[instance].render_phase = RenderPhase::AfterVehicles;
            scene.instances[instance].casts_shadow = false;
            scene.instances[instance].surface_bias = false;
            self.instances.push(instance);
        }

        for (slot, p) in wanted.iter().enumerate() {
            let instance = self.instances[slot];
            let z = world
                .walk_height(p.x, p.y)
                .filter(|z| (z - p.z).abs() < 0.75)
                .map(|surface| surface + p.height_offset_meters)
                .unwrap_or(p.z);
            let maneuver = matches!(
                p.kind.as_str(),
                "left" | "right" | "slight-left" | "slight-right" | "uturn"
            );
            let pulse = if maneuver || p.kind == "rejoin" {
                1.0 + 0.035 * (seconds * std::f32::consts::TAU * 0.8).sin() as f64
            } else {
                1.0
            };
            let semantic_scale = match p.kind.as_str() {
                "left" | "right" | "slight-left" | "slight-right" | "uturn" => 1.10 * pulse,
                "rejoin" => 1.06 * pulse,
                _ => 1.0,
            };
            let semantic_alpha = match p.kind.as_str() {
                "left" | "right" | "slight-left" | "slight-right" | "uturn" => 1.0,
                "rejoin" => 0.96,
                _ => 0.84,
            };
            renderer.set_transform(
                scene,
                instance,
                DVec3::new(p.x, p.y, z),
                transform(
                    p.heading_degrees,
                    p.width_meters * semantic_scale,
                    p.length_meters * semantic_scale,
                ),
            );
            renderer.set_params(
                scene,
                instance,
                &[(p.opacity * semantic_alpha).clamp(0.0, 1.0) as f32],
                true,
                &[],
            );
        }
        for &instance in self.instances.iter().skip(wanted.len()) {
            renderer.set_params(scene, instance, &[], false, &[]);
        }
        self.active = wanted.len();
    }

    pub(crate) fn any(&self) -> bool {
        self.active > 0
    }

    pub(crate) fn clear(
        &mut self,
        _world: &crate::scene::World,
        renderer: &Renderer,
        scene: &mut Scene,
    ) {
        for &instance in &self.instances {
            renderer.set_params(scene, instance, &[], false, &[]);
        }
        self.active = 0;
    }
}
'@
}

Set-Content -LiteralPath (Join-Path $OpenOmsiSource "crates/omsi-app/src/navbr_world_guidance.rs") -Value $world -Encoding UTF8 -NoNewline

Write-Host "NavBR openOMSI host integration applied ($WorldGuidanceRenderer world guidance)."
