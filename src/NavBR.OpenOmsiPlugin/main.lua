-- NavBR companion for openOMSI.
-- Uses only the documented Lua API exposed by openOMSI.
-- No process memory, native offsets, sockets or external file access.

local SNAPSHOT_VERSION = 2
local NEARBY_RADIUS_METERS = 1200
local MAX_NEARBY_VEHICLES = 128
local last_publish_at = -1000

local function pct(value)
  if value == nil then return "" end
  local text = tostring(value)
  return (text:gsub("([^%w%-%._~])", function(ch)
    return string.format("%%%02X", string.byte(ch))
  end))
end

local function num(value)
  if type(value) ~= "number" then return "" end
  return string.format("%.12g", value)
end

local function flag(value)
  return value and "1" or "0"
end

local function nearby_snapshot()
  if not omsi.has_vehicle() then
    return ""
  end

  local nearby = omsi.others(NEARBY_RADIUS_METERS) or {}
  table.sort(nearby, function(a, b)
    return tostring(a.id or "") < tostring(b.id or "")
  end)

  local rows = {}
  local limit = math.min(#nearby, MAX_NEARBY_VEHICLES)
  for index = 1, limit do
    local item = nearby[index]
    local speed = nil
    if item.id ~= nil then
      speed = omsi.other_var(item.id, "Velocity")
    end

    rows[#rows + 1] = table.concat({
      pct(item.id),
      pct(item.kind),
      pct(item.name),
      num(item.x),
      num(item.y),
      num(item.z),
      num(item.heading),
      num(speed)
    }, ",")
  end

  return table.concat(rows, ";")
end

local function publish(force)
  local info = omsi.info()
  local now = omsi.time()
  local speed = type(info.speed) == "number" and math.abs(info.speed) or 0
  local active = info.multiplayer or speed > 0.5
  local interval = active and 0.5 or 1.0

  if not force and (now - last_publish_at) < interval then
    return
  end
  last_publish_at = now

  local x, y, z, heading = omsi.position()

  omsi.data.navbr_seq = (omsi.data.navbr_seq or 0) + 1
  omsi.data.navbr_snapshot = table.concat({
    tostring(SNAPSHOT_VERSION),
    tostring(omsi.data.navbr_seq),
    tostring(os.time()),
    x ~= nil and "1" or "0",
    num(x),
    num(y),
    num(z),
    num(heading),
    pct(info.map),
    pct(info.line),
    pct(info.tour),
    pct(info.trip),
    pct(info.terminus),
    pct(info.next_stop),
    pct(info.view),
    flag(info.on_foot),
    flag(info.paused),
    num(info.delay),
    pct(omsi.vehicle()),
    num(info.clock),
    num(info.day),
    num(info.year),
    flag(info.multiplayer),
    num(info.traffic),
    num(info.speed),
    num(info.next_stop_arrival),
    num(info.next_stop_departure),
    nearby_snapshot()
  }, "|")

  -- openOMSI's Lua sandbox intentionally exposes no arbitrary filesystem or socket API.
  -- Persisting the compact state is therefore the documented bridge from the Lua-only
  -- world-position/nearby-vehicle API to the native NavBR plugin. The native side caches
  -- the file and only reparses it when the write timestamp changes.
  omsi.save()
end

omsi.every(0.25, function()
  publish(false)
end)

omsi.on("start", function()
  publish(true)
end)

omsi.on("vehicle", function()
  publish(true)
end)

omsi.on("duty", function()
  publish(true)
end)

omsi.on("next_stop", function()
  publish(true)
end)

omsi.on("view", function()
  publish(true)
end)
