-- Choom Co-op Phase 0 research probes (dev only, CET).
-- Install: copy this folder to <game>/bin/x64/plugins/cyber_engine_tweaks/mods/ChoomCoopProbes/
-- Use: open the CET overlay; a "ChoomCoop Probes" window has one button per probe.
-- Every result is printed to the CET console with the prefix [ChoomCoop:Probe].
-- Each probe is wrapped in pcall: if one errors, copy the error text into your report.
-- Use a throwaway test save. P3 adds a save lock; press "P3 remove lock" afterwards
-- (reloading a save also clears it).

local function log(msg) print("[ChoomCoop:Probe] " .. tostring(msg)) end

local function run(name, fn)
    local ok, err = pcall(fn)
    if not ok then log(name .. " ERROR: " .. tostring(err)) end
end

local function fmt(v) return string.format("(%.1f, %.1f, %.1f)", v.x, v.y, v.z) end

-- P1 state: poll after teleport until navmesh is streamed at the destination.
local p1 = nil

local probes = {}

probes.P1 = function()
    local player = Game.GetPlayer()
    local pos = player:GetWorldPosition()
    local target = Vector4.new(pos.x + 400, pos.y + 400, pos.z + 30, 1)
    log("P1 teleport from " .. fmt(pos) .. " to " .. fmt(target))
    Game.GetTeleportationFacility():Teleport(player, target, EulerAngles.new(0, 0, 0))
    p1 = { start = os.clock(), target = target, lastLog = -1 }
end

probes.P2 = function()
    local player = Game.GetPlayer()
    local pos = player:GetWorldPosition()
    local res = Game.GetNavigationSystem():FindPointInSphereOnlyHumanNavmesh(pos, 3.0, 0, false) -- 0 = NavGenAgentSize.Human
    log("P2 from " .. fmt(pos) .. " status=" .. tostring(res.status) .. " point=" .. fmt(res.point))
    log("P2 navmesh streamed here: " .. tostring(Game.GetNavigationSystem():IsNavmeshStreamedInLocation(pos, 1.0)))
end

probes.P3 = function()
    GetSingleton("SaveLocksManager"):RequestSaveLockAdd(Game.GetPlayer():GetGame(), "ChoomCoopProbe")
    log("P3 save lock ADDED. Now try: pause menu Save, quicksave (F5), fast travel (autosave). Report what happened.")
end

probes.P3_remove = function()
    GetSingleton("SaveLocksManager"):RequestSaveLockRemove(Game.GetPlayer():GetGame(), "ChoomCoopProbe")
    log("P3 save lock REMOVED")
end

local function psm()
    local player = Game.GetPlayer()
    local defs = Game.GetAllBlackboardDefs().PlayerStateMachine
    local bb = Game.GetBlackboardSystem():GetLocalInstanced(player:GetEntityID(), defs)
    return bb, defs
end

probes.P4 = function()
    local bb, defs = psm()
    log("P4 HighLevel=" .. tostring(bb:GetInt(defs.HighLevel))
        .. " SceneTier=" .. tostring(bb:GetInt(defs.SceneTier))
        .. " Combat=" .. tostring(bb:GetInt(defs.Combat))
        .. " Vehicle=" .. tostring(bb:GetInt(defs.Vehicle)))
    log("P4 (gamePSMHighLevel: 0 Default, 1..5 SceneTier1..5, 6 Swimming)")
end

probes.P5 = function()
    local player = Game.GetPlayer()
    local pos = player:GetWorldPosition()
    local spec = DynamicEntitySpec.new()
    spec.recordID = TweakDBID.new("Character.MaMuppet")
    spec.position = Vector4.new(pos.x + 2, pos.y, pos.z, 1)
    spec.orientation = EulerAngles.new(0, 0, 0):ToQuat()
    spec.alwaysSpawned = true
    spec.persistState = false
    spec.persistSpawn = false
    spec.tags = { "ChoomCoop.Probe" }
    local id = Game.GetDynamicEntitySystem():CreateEntity(spec)
    log("P5 spawned muppet entity hash=" .. tostring(EntityID.GetHash(id)) .. " 2m east of you. Does it stand still? Does it react to you?")
end

probes.P6 = function()
    local target = Game.GetTargetingSystem():GetLookAtObject(Game.GetPlayer(), false, false)
    if target == nil then log("P6 look at an NPC first") return end
    Game.GetStatPoolsSystem():RequestSettingStatPoolValue(target:GetEntityID(), gamedataStatPoolType.Health, 0, Game.GetPlayer(), false, false)
    log("P6 set Health=0 on " .. tostring(target:GetClassName()) .. ". Did it die normally (ragdoll, loot)?")
end

probes.P7 = function()
    local msg = SimpleScreenMessage.new()
    msg.message = "[ChoomCoop] probe notification"
    msg.isShown = true
    msg.duration = 4.0
    local bbDef = Game.GetAllBlackboardDefs().UI_Notifications
    Game.GetBlackboardSystem():Get(bbDef):SetVariant(bbDef.OnscreenMessage, ToVariant(msg), true)
    log("P7 posted on-screen message. Did yellow text appear?")
end

probes.P8 = function()
    local qs = Game.GetQuestsSystem()
    -- A well-known fact; 1 after the heist starts. Any value proves the call works.
    log("P8 fact q005_done=" .. tostring(qs:GetFactStr("q005_done")))
end

registerForEvent("onUpdate", function()
    if p1 == nil then return end
    run("P1", function()
        local t = os.clock() - p1.start
        local streamed = Game.GetNavigationSystem():IsNavmeshStreamedInLocation(p1.target, 50.0)
        local second = math.floor(t * 4) / 4
        if second ~= p1.lastLog then
            p1.lastLog = second
            log(string.format("P1 t=%.2fs navmeshStreamed=%s pos=%s", t, tostring(streamed), fmt(Game.GetPlayer():GetWorldPosition())))
        end
        if streamed or t > 15 then
            log(string.format("P1 done after %.2fs streamed=%s. Did you fall through the world?", t, tostring(streamed)))
            p1 = nil
        end
    end)
end)

local overlay = false
registerForEvent("onOverlayOpen", function() overlay = true end)
registerForEvent("onOverlayClose", function() overlay = false end)

registerForEvent("onDraw", function()
    if not overlay then return end
    if ImGui.Begin("ChoomCoop Probes") then
        local order = { "P1", "P2", "P3", "P3_remove", "P4", "P5", "P6", "P7", "P8" }
        local labels = {
            P1 = "P1 teleport 560m + streaming wait",
            P2 = "P2 navmesh point near me",
            P3 = "P3 add save lock",
            P3_remove = "P3 remove save lock",
            P4 = "P4 print scene tier / combat",
            P5 = "P5 spawn muppet puppet",
            P6 = "P6 kill looked-at NPC",
            P7 = "P7 on-screen notification",
            P8 = "P8 read quest fact",
        }
        for _, key in ipairs(order) do
            if ImGui.Button(labels[key]) then run(key, probes[key]) end
        end
    end
    ImGui.End()
end)

registerForEvent("onInit", function() log("probes loaded") end)
