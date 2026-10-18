#include <shellapi.h>
#include <ArchiveXL/support/red4ext/ArchiveXL.hpp>
#include <TweakXL/support/red4ext/TweakXL.hpp>
#include <App/Settings.h>
#include <RED4ext/LaunchParameters.hpp>

#include "RED4ext/Api/EMainReason.hpp"

extern void CoreStubsInit();

std::filesystem::path GCyberpunkMpLocation;

EXTERN_C IMAGE_DOS_HEADER __ImageBase;

void Initialize()
{
    namespace fs = std::filesystem;

    constexpr auto pathLength = MAX_PATH + 1;
    std::string filename;
    do
    {
        filename.resize(filename.size() + pathLength, '\0');

        const auto length = GetModuleFileName((HINSTANCE)&__ImageBase, filename.data(), static_cast<uint32_t>(filename.size()));
        if (length > 0)
        {
            filename.resize(length);
        }
    } while (GetLastError() == ERROR_INSUFFICIENT_BUFFER);

    fs::path path = filename;
    if (is_symlink(path))
    {
        path = read_symlink(path);
    }

    GCyberpunkMpLocation = path.parent_path();
}

#include "App/Application.h"

RED4EXT_C_EXPORT bool Main(RED4ext::PluginHandle aHandle, RED4ext::EMainReason aReason, const RED4ext::Sdk* aSdk)
{

    switch (aReason)
    {
    case RED4ext::EMainReason::Load:
    {
        Initialize();

        App::GApplication = MakeUnique<App::Application>(aHandle, aSdk);
        App::GApplication->Bootstrap();

        const auto scriptPath = GCyberpunkMpLocation / TP_REDSCRIPT_LOCATION;
        // ChoomCoop: name the missing folder before canonical() throws (otherwise RED4ext only
        // says the plugin failed and the game silently runs without the mod), and keep the
        // canonical path alive: .c_str() on the temporary read freed memory.
        // Credit: ofmiceandcam98-eng/CyberpunkMP 58881c8.
        std::error_code scriptEc;
        if (!std::filesystem::exists(scriptPath, scriptEc))
            aSdk->logger->ErrorF(aHandle, "CyberpunkMP cannot find its redscript folder at '%s' - the mod will not load.", scriptPath.string().c_str());

        const auto scriptLocation = canonical(scriptPath);
        aSdk->scripts->Add(aHandle, scriptLocation.c_str());

        ArchiveXL::RegisterArchives(canonical(GCyberpunkMpLocation / TP_ARCHIVES_LOCATION));
        TweakXL::RegisterTweaks(canonical(GCyberpunkMpLocation / TP_TWEAKS_LOCATION));

        const auto inputLoaderModule = LoadLibrary("input_loader");
        void(*pInputLoaderAdd)(RED4ext::PluginHandle, const wchar_t*);
        if (inputLoaderModule != nullptr && (pInputLoaderAdd = reinterpret_cast<decltype(pInputLoaderAdd)>(GetProcAddress(inputLoaderModule, "Add"))))
        {
            const auto inputPath = canonical(GCyberpunkMpLocation / TP_INPUTS_LOCATION); // ChoomCoop: same dangling c_str() fix
            pInputLoaderAdd(aHandle, inputPath.c_str());
        }
        else
        {
            const auto message =
                L"The following CyperpunkMP requirements were not met:\n\n* Input Loader v0.2.0\nPlease ensure the mods "
                L"above are installed/up-to-date.";
            MessageBoxW(nullptr, message, L"CyperpunkMP requirements could not be found", MB_SYSTEMMODAL | MB_ICONERROR);
            return false;
        }

        for (auto& mod : Settings::Get().mods)
        {
            aSdk->scripts->Add(aHandle, mod.wstring().c_str());
            ArchiveXL::RegisterArchives(mod);
            TweakXL::RegisterTweaks(mod);
            pInputLoaderAdd(aHandle, mod.wstring().c_str());
        }

        break;
    }
    case RED4ext::EMainReason::Unload:
    {
        if (Settings::IsDisabled())
            return false;

        App::GApplication->Shutdown();
        App::GApplication = nullptr;
        break;
    }
    }

    return true;
}

RED4EXT_C_EXPORT void Query(RED4ext::PluginInfo* aInfo)
{
    aInfo->name = L"CyberpunkMP";
    aInfo->author = L"Tilted Phoques SRL";
    aInfo->version = RED4EXT_SEMVER(0, 1, 0);

    aInfo->runtime = RED4EXT_V0_RUNTIME_INDEPENDENT;
    aInfo->sdk = RED4EXT_SDK_LATEST;
}

RED4EXT_C_EXPORT uint32_t Supports()
{
    return RED4EXT_API_VERSION_LATEST;
}
