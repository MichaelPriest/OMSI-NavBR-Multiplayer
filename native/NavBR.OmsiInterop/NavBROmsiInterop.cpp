#include <windows.h>
#include <cstdint>
#include <cstring>
#include <cwchar>

#if !defined(_M_IX86)
#error NavBR.OmsiInterop must be compiled for x86.
#endif

namespace
{
    constexpr std::uintptr_t PreferredImageBase = 0x00400000u;

    constexpr std::uintptr_t RvaGetMem = 0x00404614u - PreferredImageBase;
    constexpr std::uintptr_t RvaFreeMem = 0x00404630u - PreferredImageBase;
    constexpr std::uintptr_t RvaSetCriticalSectionLock = 0x00562AF8u - PreferredImageBase;
    constexpr std::uintptr_t RvaReleaseCriticalSectionLock = 0x00562B30u - PreferredImageBase;
    constexpr std::uintptr_t RvaMakeVehicle = 0x0070A250u - PreferredImageBase;
    constexpr std::uintptr_t RvaTempRvListCreate = 0x0074A0E0u - PreferredImageBase;
    constexpr std::uintptr_t RvaCopyTempListIntoMainList = 0x0074A240u - PreferredImageBase;

    constexpr std::uintptr_t RvaTempRvListClass = 0x0074802Cu - PreferredImageBase;
    constexpr std::uintptr_t RvaRoadVehiclesPointer = 0x00861508u - PreferredImageBase;
    constexpr std::uintptr_t RvaRoadVehicleTypesPointer = 0x008615A8u - PreferredImageBase;
    constexpr std::uintptr_t RvaProgramManagerPointer = 0x00862F28u - PreferredImageBase;

    constexpr int ProgramManagerMakeVehicleCriticalSectionOffset = 0x1B4;
    constexpr int RoadVehicleListItemsOffset = 0x28;
    constexpr int RoadVehicleListCountOffset = 0x2C;
    constexpr int ObjectListItemsPointerOffset = 0x04;
    constexpr int MaxReasonableRoadVehicles = 4096;

    std::uintptr_t ImageBase()
    {
        return reinterpret_cast<std::uintptr_t>(GetModuleHandleW(nullptr));
    }

    std::uintptr_t Resolve(std::uintptr_t rva)
    {
        const auto imageBase = ImageBase();
        return imageBase == 0 ? 0 : imageBase + rva;
    }

    bool IsReadableRange(std::uintptr_t address, std::size_t bytes)
    {
        if (address == 0 || bytes == 0)
        {
            return false;
        }

        MEMORY_BASIC_INFORMATION info{};
        if (VirtualQuery(reinterpret_cast<const void*>(address), &info, sizeof(info)) == 0)
        {
            return false;
        }

        if (info.State != MEM_COMMIT || (info.Protect & (PAGE_GUARD | PAGE_NOACCESS)) != 0)
        {
            return false;
        }

        const auto regionStart = reinterpret_cast<std::uintptr_t>(info.BaseAddress);
        const auto regionEnd = regionStart + info.RegionSize;
        if (regionEnd < regionStart || address < regionStart)
        {
            return false;
        }

        return bytes <= regionEnd - address;
    }

    bool IsReadableAddress(std::uintptr_t address)
    {
        return IsReadableRange(address, sizeof(std::uint32_t));
    }

    bool IsExecutableAddress(std::uintptr_t address)
    {
        if (address == 0)
        {
            return false;
        }

        MEMORY_BASIC_INFORMATION info{};
        if (VirtualQuery(reinterpret_cast<const void*>(address), &info, sizeof(info)) == 0 ||
            info.State != MEM_COMMIT ||
            (info.Protect & (PAGE_GUARD | PAGE_NOACCESS)) != 0)
        {
            return false;
        }

        switch (info.Protect & 0xFFu)
        {
        case PAGE_EXECUTE:
        case PAGE_EXECUTE_READ:
        case PAGE_EXECUTE_READWRITE:
        case PAGE_EXECUTE_WRITECOPY:
            return true;
        default:
            return false;
        }
    }

    int ReadPointerAtRva(std::uintptr_t rva)
    {
        const auto address = Resolve(rva);
        if (!IsReadableRange(address, sizeof(int)))
        {
            return 0;
        }

        return *reinterpret_cast<const int*>(address);
    }

    int GetRoadVehicleMainList()
    {
        return ReadPointerAtRva(RvaRoadVehiclesPointer);
    }

    bool TryGetRoadVehicleItems(int& count, int& itemArray)
    {
        count = 0;
        itemArray = 0;

        const int mainList = GetRoadVehicleMainList();
        if (mainList == 0 ||
            !IsReadableRange(
                static_cast<std::uintptr_t>(mainList) + RoadVehicleListItemsOffset,
                sizeof(int)) ||
            !IsReadableRange(
                static_cast<std::uintptr_t>(mainList) + RoadVehicleListCountOffset,
                sizeof(int)))
        {
            return false;
        }

        const int currentCount = *reinterpret_cast<const int*>(
            static_cast<std::uintptr_t>(mainList) + RoadVehicleListCountOffset);
        const int objectList = *reinterpret_cast<const int*>(
            static_cast<std::uintptr_t>(mainList) + RoadVehicleListItemsOffset);

        if (currentCount < 0 || currentCount > MaxReasonableRoadVehicles)
        {
            return false;
        }

        if (currentCount == 0)
        {
            count = 0;
            itemArray = 0;
            return true;
        }

        if (objectList == 0 ||
            !IsReadableRange(
                static_cast<std::uintptr_t>(objectList) + ObjectListItemsPointerOffset,
                sizeof(int)))
        {
            return false;
        }

        const int items = *reinterpret_cast<const int*>(
            static_cast<std::uintptr_t>(objectList) + ObjectListItemsPointerOffset);
        if (items == 0 ||
            !IsReadableRange(
                static_cast<std::uintptr_t>(items),
                static_cast<std::size_t>(currentCount) * sizeof(int)))
        {
            return false;
        }

        count = currentCount;
        itemArray = items;
        return true;
    }

    int CallGetMem(int bytes)
    {
        if (bytes <= 0)
        {
            return 0;
        }

        const auto target = Resolve(RvaGetMem);
        if (!IsExecutableAddress(target))
        {
            return 0;
        }

        int result = 0;
        __asm
        {
            mov eax, bytes
            mov edx, target
            call edx
            mov result, eax
        }
        return result;
    }

    int CallFreeMem(int address)
    {
        if (address == 0)
        {
            return 0;
        }

        const auto target = Resolve(RvaFreeMem);
        if (!IsExecutableAddress(target))
        {
            return 0;
        }

        int result = 0;
        __asm
        {
            mov eax, address
            mov edx, target
            call edx
            mov result, eax
        }
        return result;
    }

    bool CallCriticalSection(std::uintptr_t target, int criticalSectionAddress)
    {
        if (!IsExecutableAddress(target) ||
            !IsReadableRange(static_cast<std::uintptr_t>(criticalSectionAddress), sizeof(int)))
        {
            return false;
        }

        __asm
        {
            mov eax, criticalSectionAddress
            mov edx, target
            call edx
        }
        return true;
    }
}

extern "C" __declspec(dllexport) int __cdecl NavBR_GetAbiVersion()
{
    return 1;
}

extern "C" __declspec(dllexport) unsigned int __cdecl NavBR_GetImageBase()
{
    return static_cast<unsigned int>(ImageBase());
}

extern "C" __declspec(dllexport) int __cdecl NavBR_ProbeOmsi23004Addresses()
{
    const auto imageBase = ImageBase();
    if (imageBase == 0 || sizeof(void*) != 4)
    {
        return 0;
    }

    if (!IsExecutableAddress(Resolve(RvaGetMem)) ||
        !IsExecutableAddress(Resolve(RvaFreeMem)) ||
        !IsExecutableAddress(Resolve(RvaSetCriticalSectionLock)) ||
        !IsExecutableAddress(Resolve(RvaReleaseCriticalSectionLock)) ||
        !IsExecutableAddress(Resolve(RvaMakeVehicle)) ||
        !IsExecutableAddress(Resolve(RvaTempRvListCreate)) ||
        !IsExecutableAddress(Resolve(RvaCopyTempListIntoMainList)) ||
        !IsReadableAddress(Resolve(RvaTempRvListClass)) ||
        !IsReadableAddress(Resolve(RvaRoadVehiclesPointer)) ||
        !IsReadableAddress(Resolve(RvaRoadVehicleTypesPointer)) ||
        !IsReadableAddress(Resolve(RvaProgramManagerPointer)))
    {
        return 0;
    }

    return 1;
}

extern "C" __declspec(dllexport) int __cdecl NavBR_GetProgramManager()
{
    return ReadPointerAtRva(RvaProgramManagerPointer);
}

extern "C" __declspec(dllexport) int __cdecl NavBR_GetRoadVehicleTypes()
{
    return ReadPointerAtRva(RvaRoadVehicleTypesPointer);
}

extern "C" __declspec(dllexport) int __cdecl NavBR_GetRoadVehicleCount()
{
    int count = 0;
    int items = 0;
    return TryGetRoadVehicleItems(count, items) ? count : -1;
}

extern "C" __declspec(dllexport) int __cdecl NavBR_GetRoadVehicleAt(int index)
{
    int count = 0;
    int items = 0;
    if (!TryGetRoadVehicleItems(count, items) || index < 0 || index >= count)
    {
        return 0;
    }

    return *reinterpret_cast<const int*>(
        static_cast<std::uintptr_t>(items) +
        static_cast<std::uintptr_t>(index) * sizeof(int));
}

extern "C" __declspec(dllexport) int __cdecl NavBR_GetMem(int bytes)
{
    return CallGetMem(bytes);
}

extern "C" __declspec(dllexport) int __cdecl NavBR_FreeMem(int address)
{
    return CallFreeMem(address);
}

extern "C" __declspec(dllexport) int __cdecl NavBR_LockMakeVehicle(int programManager)
{
    if (programManager == 0)
    {
        return 0;
    }

    const int criticalSectionAddress =
        programManager + ProgramManagerMakeVehicleCriticalSectionOffset;
    return CallCriticalSection(
        Resolve(RvaSetCriticalSectionLock),
        criticalSectionAddress)
        ? 1
        : 0;
}

extern "C" __declspec(dllexport) int __cdecl NavBR_UnlockMakeVehicle(int programManager)
{
    if (programManager == 0)
    {
        return 0;
    }

    const int criticalSectionAddress =
        programManager + ProgramManagerMakeVehicleCriticalSectionOffset;
    return CallCriticalSection(
        Resolve(RvaReleaseCriticalSectionLock),
        criticalSectionAddress)
        ? 1
        : 0;
}

extern "C" __declspec(dllexport) int __cdecl NavBR_AllocateAnsiString(const wchar_t* value)
{
    if (value == nullptr)
    {
        return 0;
    }

    const auto characterLength = std::wcslen(value);
    if (characterLength > 4096u)
    {
        return 0;
    }

    BOOL usedDefaultCharacter = FALSE;
    const int byteLength = WideCharToMultiByte(
        1252,
        WC_NO_BEST_FIT_CHARS,
        value,
        static_cast<int>(characterLength),
        nullptr,
        0,
        nullptr,
        &usedDefaultCharacter);
    if ((characterLength > 0u && byteLength <= 0) || usedDefaultCharacter)
    {
        return 0;
    }

    const auto allocationSize = byteLength + 13;
    const int allocation = CallGetMem(allocationSize);
    if (allocation == 0)
    {
        return 0;
    }

    auto* header = reinterpret_cast<unsigned char*>(allocation);
    const std::uint16_t codePage = 1252;
    const std::uint16_t characterSize = 1;
    const std::int32_t referenceCount = 1;
    const std::int32_t delphiLength = byteLength;

    std::memcpy(header + 0, &codePage, sizeof(codePage));
    std::memcpy(header + 2, &characterSize, sizeof(characterSize));
    std::memcpy(header + 4, &referenceCount, sizeof(referenceCount));
    std::memcpy(header + 8, &delphiLength, sizeof(delphiLength));

    if (byteLength > 0)
    {
        usedDefaultCharacter = FALSE;
        const int written = WideCharToMultiByte(
            1252,
            WC_NO_BEST_FIT_CHARS,
            value,
            static_cast<int>(characterLength),
            reinterpret_cast<char*>(header + 12),
            byteLength,
            nullptr,
            &usedDefaultCharacter);
        if (written != byteLength || usedDefaultCharacter)
        {
            CallFreeMem(allocation);
            return 0;
        }
    }

    header[12 + byteLength] = 0;
    return allocation + 12;
}

extern "C" __declspec(dllexport) int __cdecl NavBR_FreeAnsiString(int stringData)
{
    if (stringData <= 12)
    {
        return 0;
    }

    return CallFreeMem(stringData - 12);
}

extern "C" __declspec(dllexport) int __cdecl NavBR_TempRoadVehicleListCreate(int capacity)
{
    if (capacity <= 0 || capacity > 256)
    {
        return 0;
    }

    const int classAddress = static_cast<int>(Resolve(RvaTempRvListClass));
    const auto target = Resolve(RvaTempRvListCreate);
    if (classAddress == 0 || !IsExecutableAddress(target))
    {
        return 0;
    }

    int result = 0;
    __asm
    {
        mov eax, classAddress
        mov edx, capacity
        mov ecx, target
        call ecx
        mov result, eax
    }
    return result;
}

extern "C" __declspec(dllexport) int __cdecl NavBR_CopyTempRoadVehicleListIntoMain(int tempList)
{
    if (tempList == 0)
    {
        return 0;
    }

    const int mainList = GetRoadVehicleMainList();
    const auto target = Resolve(RvaCopyTempListIntoMainList);
    if (mainList == 0 || !IsExecutableAddress(target))
    {
        return 0;
    }

    int result = 0;
    __asm
    {
        mov eax, mainList
        mov edx, tempList
        mov ecx, target
        call ecx
        mov result, eax
    }
    return result;
}

extern "C" __declspec(dllexport) int __cdecl NavBR_MakeVehicle(
    int programManager,
    int vehicleList,
    int roadVehicleTypes,
    int onlyVehicleList,
    int cs,
    int timetableTimeBits,
    int situationLoad,
    int dialog,
    int setDriver,
    int thread,
    int licensePlateIndex,
    int initCall,
    int startDay,
    int trainBuildDirection,
    int reverse,
    int groupHof,
    int vehicleType,
    int tour,
    int line,
    int paintScheme,
    int scheduled,
    int aiRoadVehicle,
    int randomLicensePlate,
    int randomPaintScheme,
    int filenameAnsiString)
{
    const auto target = Resolve(RvaMakeVehicle);
    if (!IsExecutableAddress(target) ||
        programManager == 0 ||
        vehicleList == 0 ||
        roadVehicleTypes == 0 ||
        filenameAnsiString == 0)
    {
        return 0;
    }

    int result = 0;
    int restoreEsp = 0;
    __asm
    {
        // Preserve ESI for the cdecl caller and remember the exact stack state.
        // We restore ESP explicitly after the Delphi/Borland call so the shim
        // does not depend on undocumented assumptions about callee cleanup.
        push esi
        mov restoreEsp, esp

        // Borland register calling convention: EAX, EDX and ECX carry the
        // first three parameters. Remaining DWORDs are pushed right to left.
        push filenameAnsiString
        push randomPaintScheme
        push randomLicensePlate
        push aiRoadVehicle
        push scheduled
        push paintScheme
        push line
        push tour
        push vehicleType
        push groupHof
        push reverse
        push trainBuildDirection
        push startDay
        push initCall
        push licensePlateIndex
        push thread
        push setDriver
        push dialog
        push situationLoad
        push timetableTimeBits
        push cs
        push onlyVehicleList

        mov eax, programManager
        mov edx, vehicleList
        mov ecx, roadVehicleTypes
        mov esi, target
        call esi
        mov result, eax

        mov esp, restoreEsp
        pop esi
    }

    return result;
}
