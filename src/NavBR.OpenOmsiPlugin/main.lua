-- NavBR companion for openOMSI.
-- Uses only the documented Lua API exposed by openOMSI.
-- No process memory, native offsets, sockets or external file access.

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

local function publish()
  local info = omsi.info()
  local x, y, z, heading = omsi.position()

  omsi.data.navbr_seq = (omsi.data.navbr_seq or 0) + 1
  omsi.data.navbr_snapshot = table.concat({
    "1",
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
    pct(omsi.vehicle())
  }, "|")

  -- The sandbox only allows persistence through omsi.data. One tiny write per
  -- second is enough for NavBR map/CCO/navigation and stays off the frame path.
  omsi.save()
end

omsi.every(1.0, publish)

omsi.on("vehicle", function()
  publish()
end)

omsi.on("duty", function()
  publish()
end)

omsi.on("next_stop", function()
  publish()
end)
