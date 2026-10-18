-- Choom Co-op build targets. Included from the root xmake.lua (see docs/DECISIONS.md).

-- Server plugin: published next to upstream's plugins so Server.Loader finds it at
-- <targetdir>/plugins/ChoomCoopSystem/ChoomCoopSystem.dll. Server.Scripting's own install
-- step then copies the whole plugins folder into distrib.
target("ChoomCoop.Server")
    set_kind("phony")
    set_group("ChoomCoop")
    add_deps("Server.Scripting") -- needs the generated CyberpunkSdk
    add_extrafiles("Server/ChoomCoop.Server/**.cs", "Shared/Protocol/*.cs")

    on_build(function (target, opt)
        import("utils.progress")
        local proj = path.join(target:scriptdir(), "Server", "ChoomCoop.Server", "ChoomCoop.Server.csproj")
        local mode = is_mode("debug") and "Debug" or "Release"
        local output = path.join(target:targetdir(), "plugins", "ChoomCoopSystem")
        progress.show(opt.progress, "${color.build.target}build choomcoop server plugin")
        os.run("dotnet publish " .. proj .. " -c " .. mode .. " -o " .. output)
    end)

    on_install(function (target)
        -- Server.Scripting installs everything under <targetdir>/plugins.
    end)

-- Client redscript: installed in the game's own layout so it can be dropped into the
-- game folder: <installdir>/choomcoop/r6/scripts/ChoomCoop/...
target("ChoomCoop.Scripts")
    set_kind("phony")
    set_group("ChoomCoop")
    add_extrafiles("Scripts/**.reds", "Shared/Protocol/*.reds")

    on_install(function (target)
        local root = target:scriptdir()
        local dest = path.join(target:installdir(), "choomcoop", "r6", "scripts", "ChoomCoop")
        if os.isdir(dest) then
            os.rmdir(dest)
        end
        for _, file in ipairs(os.files(path.join(root, "Scripts", "**.reds"))) do
            os.cp(file, path.join(dest, path.relative(file, path.join(root, "Scripts"))))
        end
        for _, file in ipairs(os.files(path.join(root, "Shared", "Protocol", "*.reds"))) do
            os.cp(file, path.join(dest, "Protocol", path.filename(file)))
        end
        print("ChoomCoop scripts installed to " .. dest)
    end)
