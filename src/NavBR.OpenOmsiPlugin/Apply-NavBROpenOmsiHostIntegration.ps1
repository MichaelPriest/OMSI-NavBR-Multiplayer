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
$ui = Join-Path $OpenOmsiSource "crates/omsi-app/src/ui.rs"

# --- omsi-plugin: optional in-process renderer export.
Replace-Required $plugin @'
type AccessTriggerFn = unsafe extern "system" fn(u16, *mut u8);
'@ @'
type AccessTriggerFn = unsafe extern "system" fn(u16, *mut u8);
type OverlayFrameFn = unsafe extern "system" fn(*mut u8, i32) -> i32;
'@

Replace-Required $plugin @'
    string: Option<AccessStringFn>,
}
'@ @'
    string: Option<AccessStringFn>,
    overlay_frame_v2: Option<OverlayFrameFn>,
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
            Ok(Library { _lib: lib, start, finalize, variable, trigger, system, string, overlay_frame_v2 })
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
                        self.navbr_world_guidance.tick(dt, w, r, scene, frame);
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
    /// What kind of menu
'@ @'
    pub notice_anchor: Option<[f32; 4]>,
    pub navbr_overlay: Option<&'a crate::navbr_overlay::Overlay2D>,
    /// What kind of menu
'@

Replace-Required $events @'
                            notices: &self.notices,
                            notice_anchor: self.navigator.as_ref().and_then(|n| n.screen_rect()),
'@ @'
                            notices: &self.notices,
                            notice_anchor: self.navigator.as_ref().and_then(|n| n.screen_rect()),
                            navbr_overlay: self.navbr_overlay.overlay_2d.as_ref(),
'@

Replace-Required $ui @'
        if let Some(fps) = f.fps {
'@ @'
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

            let mut h = p.h as f32 + pad * 2.0;
            if !detail.is_empty() { h += d.h as f32 + 4.0 * s; }
            if !remaining.is_empty() { h += rem.h as f32 + 3.0 * s; }

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
        self.timestamp = frame.timestamp_unix_milliseconds;
        self.overlay_2d = Some(frame.overlay_2_d);
        self.world = Some(frame.world_guidance);
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
    pub(crate) primary_text: Option<String>,
    pub(crate) secondary_text: Option<String>,
    pub(crate) maneuver_icon: Option<String>,
    pub(crate) distance_to_maneuver_meters: Option<f64>,
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
use omsi_render::{AlphaMode, MaterialId, MeshId, Renderer, Scene};

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
            renderer.add_material(scene, None, AlphaMode::Blend, [0.10, 0.72, 1.00, 0.88], true)
        });
        (mesh, material)
    }

    pub(crate) fn tick(
        &mut self,
        _dt: f32,
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
            self.instances.push(renderer.add_instance(
                scene,
                mesh,
                DVec3::ZERO,
                Mat4::IDENTITY,
                vec![material],
            ));
        }

        for (slot, p) in wanted.iter().enumerate() {
            let instance = self.instances[slot];
            let z = world
                .walk_height(p.x, p.y)
                .filter(|z| (z - p.z).abs() < 0.75)
                .unwrap_or(p.z);
            renderer.set_transform(
                scene,
                instance,
                DVec3::new(p.x, p.y, z),
                transform(p.heading_degrees, p.width_meters, p.length_meters),
            );
            renderer.set_params(
                scene,
                instance,
                &[p.opacity.clamp(0.0, 1.0) as f32],
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
