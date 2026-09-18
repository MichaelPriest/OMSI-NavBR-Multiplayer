#define NOMINMAX
#include <windows.h>
#include <cmath>
#include <cstdint>
#include <cstring>

#if !defined(_M_IX86)
#error NavBR.OmsiInterop must be compiled for x86.
#endif

namespace
{
    constexpr std::uintptr_t PreferredImageBase = 0x00400000u;
    constexpr std::uintptr_t RvaRoadVehiclesPointer = 0x00861508u - PreferredImageBase;
    constexpr std::uintptr_t RvaHumansPointer = 0x0086172Cu - PreferredImageBase;
    constexpr std::uintptr_t RvaPlayerVehicleIndex = 0x00861740u - PreferredImageBase;

    constexpr int RoadVehicleListItemsOffset = 0x28;
    constexpr int RoadVehicleListCountOffset = 0x2C;
    constexpr int ObjectListItemsPointerOffset = 0x04;
    constexpr int MaxReasonableRoadVehicles = 4096;
    constexpr int MaxReasonableHumans = 8192;

    constexpr int PositionOffset = 0x004;
    constexpr int RotationOffset = 0x050;
    constexpr int MarkedForKillingOffset = 0x25C;
    constexpr int LastPositionOffset = 0x26E;
    constexpr int LastRotationOffset = 0x27A;
    constexpr int TachoOffset = 0x424;
    constexpr int GroundspeedOffset = 0x428;
    constexpr int PaiOffset = 0x624;
    constexpr int AiLightOffset = 0x634;
    constexpr int AiInteriorLightOffset = 0x638;
    constexpr int AiBlinkerLeftOffset = 0x63C;
    constexpr int AiBlinkerRightOffset = 0x640;
    constexpr int AiBrakeLightOffset = 0x644;

    // OmsiHumanBeingInst offsets documented by public OMSI reverse-engineering
    // references. These are guarded by membership in the global Humans array.
    constexpr int HumanDefinitionOffset = 0x5B0;
    constexpr int HumanRenderMeOffset = 0x5BC;
    constexpr int HumanInWorldOffset = 0x5EF;
    constexpr int HumanSollHeadingOffset = 0x69C;
    constexpr int HumanSollSpeedOffset = 0x6A0;
    constexpr int HumanActSpeedOffset = 0x6A4;
    constexpr int HumanActHeadingOffset = 0x6A8;
    constexpr int HumanFixDriverOffset = 0x662;
    constexpr int HumanMyBusOffset = 0x6B4;
    constexpr int HumanAiModeOffset = 0x6C4;
    constexpr int HumanAiModeExOffset = 0x6C5;
    constexpr int HumanAiSubModeOffset = 0x6C6;

    struct Vec3
    {
        float x;
        float y;
        float z;
    };

    struct Quaternion
    {
        float x;
        float y;
        float z;
        float w;
    };

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
        if (VirtualQuery(reinterpret_cast<const void*>(address), &info, sizeof(info)) == 0 ||
            info.State != MEM_COMMIT ||
            (info.Protect & (PAGE_GUARD | PAGE_NOACCESS)) != 0)
        {
            return false;
        }

        const auto regionStart = reinterpret_cast<std::uintptr_t>(info.BaseAddress);
        const auto regionEnd = regionStart + info.RegionSize;
        return regionEnd >= regionStart &&
               address >= regionStart &&
               bytes <= regionEnd - address;
    }

    bool IsWritableRange(std::uintptr_t address, std::size_t bytes)
    {
        if (!IsReadableRange(address, bytes))
        {
            return false;
        }

        MEMORY_BASIC_INFORMATION info{};
        if (VirtualQuery(reinterpret_cast<const void*>(address), &info, sizeof(info)) == 0)
        {
            return false;
        }

        switch (info.Protect & 0xFFu)
        {
        case PAGE_READWRITE:
        case PAGE_WRITECOPY:
        case PAGE_EXECUTE_READWRITE:
        case PAGE_EXECUTE_WRITECOPY:
            return true;
        default:
            return false;
        }
    }

    bool TryGetRoadVehicleItems(int& count, int& itemArray)
    {
        count = 0;
        itemArray = 0;

        const auto globalAddress = Resolve(RvaRoadVehiclesPointer);
        if (!IsReadableRange(globalAddress, sizeof(int)))
        {
            return false;
        }

        const int mainList = *reinterpret_cast<const int*>(globalAddress);
        if (mainList == 0 ||
            !IsReadableRange(static_cast<std::uintptr_t>(mainList) + RoadVehicleListItemsOffset, sizeof(int)) ||
            !IsReadableRange(static_cast<std::uintptr_t>(mainList) + RoadVehicleListCountOffset, sizeof(int)))
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
            return true;
        }

        if (objectList == 0 ||
            !IsReadableRange(static_cast<std::uintptr_t>(objectList) + ObjectListItemsPointerOffset, sizeof(int)))
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


    bool TryGetHumanItems(int& count, int& itemArray)
    {
        count = 0;
        itemArray = 0;

        const auto globalAddress = Resolve(RvaHumansPointer);
        if (!IsReadableRange(globalAddress, sizeof(int)))
        {
            return false;
        }

        const int items = *reinterpret_cast<const int*>(globalAddress);
        if (items == 0)
        {
            return true;
        }

        const auto lengthAddress = static_cast<std::uintptr_t>(items) - sizeof(int);
        if (!IsReadableRange(lengthAddress, sizeof(int)))
        {
            return false;
        }

        const int currentCount = *reinterpret_cast<const int*>(lengthAddress);
        if (currentCount < 0 || currentCount > MaxReasonableHumans)
        {
            return false;
        }

        if (currentCount > 0 &&
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

    bool IsHumanPointer(int humanPointer)
    {
        if (humanPointer <= 0)
        {
            return false;
        }

        int count = 0;
        int items = 0;
        if (!TryGetHumanItems(count, items))
        {
            return false;
        }

        for (int index = 0; index < count; ++index)
        {
            const int current = *reinterpret_cast<const int*>(
                static_cast<std::uintptr_t>(items) +
                static_cast<std::uintptr_t>(index) * sizeof(int));
            if (current == humanPointer)
            {
                return true;
            }
        }

        return false;
    }

    int GetPlayerVehiclePointer()
    {
        int count = 0;
        int items = 0;
        if (!TryGetRoadVehicleItems(count, items) || count <= 0)
        {
            return 0;
        }

        const auto indexAddress = Resolve(RvaPlayerVehicleIndex);
        if (!IsReadableRange(indexAddress, sizeof(int)))
        {
            return 0;
        }

        const int index = *reinterpret_cast<const int*>(indexAddress);
        if (index < 0 || index >= count)
        {
            return 0;
        }

        return *reinterpret_cast<const int*>(
            static_cast<std::uintptr_t>(items) +
            static_cast<std::uintptr_t>(index) * sizeof(int));
    }

    bool IsPlayerBusDriverHuman(int humanPointer, int definitionPointer)
    {
        if (!IsHumanPointer(humanPointer) || definitionPointer <= 0)
        {
            return false;
        }

        const int playerVehicle = GetPlayerVehiclePointer();
        if (playerVehicle == 0)
        {
            return false;
        }

        const auto base = static_cast<std::uintptr_t>(humanPointer);
        if (!IsReadableRange(base + HumanDefinitionOffset, sizeof(int)) ||
            !IsReadableRange(base + HumanMyBusOffset, sizeof(int)) ||
            !IsReadableRange(base + HumanAiModeExOffset, sizeof(unsigned char)) ||
            !IsReadableRange(base + HumanFixDriverOffset, sizeof(unsigned char)))
        {
            return false;
        }

        const int humanDefinition = *reinterpret_cast<const int*>(base + HumanDefinitionOffset);
        const int myBus = *reinterpret_cast<const int*>(base + HumanMyBusOffset);
        const auto aiModeEx = *reinterpret_cast<const unsigned char*>(base + HumanAiModeExOffset);
        const auto fixDriver = *reinterpret_cast<const unsigned char*>(base + HumanFixDriverOffset);

        // THAME_DrivingBus == 9 in OMSI's public enum. Some fixed driver
        // instances expose Activity_FixDriver even while their extended mode
        // is transitioning, so accept either signal.
        return humanDefinition == definitionPointer &&
               myBus == playerVehicle &&
               (aiModeEx == 9 || fixDriver != 0);
    }

    bool IsHumanControllable(int humanPointer)
    {
        if (!IsHumanPointer(humanPointer))
        {
            return false;
        }

        const auto base = static_cast<std::uintptr_t>(humanPointer);
        if (!IsReadableRange(base + PositionOffset, sizeof(Vec3)) ||
            !IsReadableRange(base + MarkedForKillingOffset, sizeof(unsigned char)) ||
            !IsReadableRange(base + HumanRenderMeOffset, sizeof(unsigned char)) ||
            !IsReadableRange(base + HumanInWorldOffset, sizeof(unsigned char)) ||
            !IsReadableRange(base + HumanMyBusOffset, sizeof(int)))
        {
            return false;
        }

        const auto position = *reinterpret_cast<const Vec3*>(base + PositionOffset);
        const auto marked = *reinterpret_cast<const unsigned char*>(base + MarkedForKillingOffset);
        const auto renderMe = *reinterpret_cast<const unsigned char*>(base + HumanRenderMeOffset);
        const auto inWorld = *reinterpret_cast<const unsigned char*>(base + HumanInWorldOffset);
        const auto myBus = *reinterpret_cast<const int*>(base + HumanMyBusOffset);

        return marked == 0 &&
               renderMe != 0 &&
               inWorld != 0 &&
               myBus == 0 &&
               std::isfinite(position.x) &&
               std::isfinite(position.y) &&
               std::isfinite(position.z);
    }

    bool ReadHumanPose(
        int humanPointer,
        float& x,
        float& y,
        float& z,
        float& heading,
        float& speed)
    {
        if (!IsHumanPointer(humanPointer))
        {
            return false;
        }

        const auto base = static_cast<std::uintptr_t>(humanPointer);
        if (!IsReadableRange(base + PositionOffset, sizeof(Vec3)) ||
            !IsReadableRange(base + HumanActHeadingOffset, sizeof(float)) ||
            !IsReadableRange(base + HumanActSpeedOffset, sizeof(float)))
        {
            return false;
        }

        const auto position = *reinterpret_cast<const Vec3*>(base + PositionOffset);
        const float currentHeading = *reinterpret_cast<const float*>(base + HumanActHeadingOffset);
        const float currentSpeed = *reinterpret_cast<const float*>(base + HumanActSpeedOffset);
        if (!std::isfinite(position.x) ||
            !std::isfinite(position.y) ||
            !std::isfinite(position.z) ||
            !std::isfinite(currentHeading) ||
            !std::isfinite(currentSpeed))
        {
            return false;
        }

        x = position.x;
        y = position.y;
        z = position.z;
        heading = currentHeading;
        speed = currentSpeed;
        return true;
    }

    bool IsRoadVehiclePointer(int vehiclePointer)
    {
        if (vehiclePointer <= 0)
        {
            return false;
        }

        int count = 0;
        int items = 0;
        if (!TryGetRoadVehicleItems(count, items))
        {
            return false;
        }

        for (int index = 0; index < count; ++index)
        {
            const int current = *reinterpret_cast<const int*>(
                static_cast<std::uintptr_t>(items) +
                static_cast<std::uintptr_t>(index) * sizeof(int));
            if (current == vehiclePointer)
            {
                return true;
            }
        }

        return false;
    }

    template <typename T>
    bool WriteValue(int vehiclePointer, int offset, const T& value)
    {
        const auto address = static_cast<std::uintptr_t>(vehiclePointer) + offset;
        if (!IsWritableRange(address, sizeof(T)))
        {
            return false;
        }

        std::memcpy(reinterpret_cast<void*>(address), &value, sizeof(T));
        return true;
    }

    bool WriteByte(int vehiclePointer, int offset, unsigned char value)
    {
        const auto address = static_cast<std::uintptr_t>(vehiclePointer) + offset;
        if (!IsWritableRange(address, sizeof(value)))
        {
            return false;
        }

        *reinterpret_cast<unsigned char*>(address) = value;
        return true;
    }
}

extern "C" __declspec(dllexport) int __cdecl NavBR_GetStateInteropVersion()
{
    return 3;
}

extern "C" __declspec(dllexport) int __cdecl NavBR_IsRoadVehiclePointer(int vehiclePointer)
{
    return IsRoadVehiclePointer(vehiclePointer) ? 1 : 0;
}


extern "C" __declspec(dllexport) int __cdecl NavBR_GetHumanCount()
{
    int count = 0;
    int items = 0;
    return TryGetHumanItems(count, items) ? count : -1;
}

extern "C" __declspec(dllexport) int __cdecl NavBR_GetHumanAt(int index)
{
    int count = 0;
    int items = 0;
    if (!TryGetHumanItems(count, items) || index < 0 || index >= count)
    {
        return 0;
    }

    return *reinterpret_cast<const int*>(
        static_cast<std::uintptr_t>(items) +
        static_cast<std::uintptr_t>(index) * sizeof(int));
}

extern "C" __declspec(dllexport) int __cdecl NavBR_IsHumanPointer(int humanPointer)
{
    return IsHumanPointer(humanPointer) ? 1 : 0;
}

extern "C" __declspec(dllexport) int __cdecl NavBR_IsHumanControllable(int humanPointer)
{
    return IsHumanControllable(humanPointer) ? 1 : 0;
}

extern "C" __declspec(dllexport) int __cdecl NavBR_GetPlayerVehiclePointer()
{
    return GetPlayerVehiclePointer();
}

extern "C" __declspec(dllexport) int __cdecl NavBR_IsPlayerBusDriverHuman(
    int humanPointer,
    int definitionPointer)
{
    return IsPlayerBusDriverHuman(humanPointer, definitionPointer) ? 1 : 0;
}

extern "C" __declspec(dllexport) int __cdecl NavBR_ReadHumanPose(
    int humanPointer,
    float* x,
    float* y,
    float* z,
    float* heading,
    float* speed)
{
    if (x == nullptr || y == nullptr || z == nullptr || heading == nullptr || speed == nullptr)
    {
        return 0;
    }

    float px = 0.0f;
    float py = 0.0f;
    float pz = 0.0f;
    float hdg = 0.0f;
    float currentSpeed = 0.0f;
    if (!ReadHumanPose(humanPointer, px, py, pz, hdg, currentSpeed))
    {
        return 0;
    }

    *x = px;
    *y = py;
    *z = pz;
    *heading = hdg;
    *speed = currentSpeed;
    return 1;
}

extern "C" __declspec(dllexport) int __cdecl NavBR_ReadHumanDriverState(
    int humanPointer,
    int* myBus,
    unsigned char* fixDriver,
    unsigned char* renderMe,
    unsigned char* inWorld)
{
    if (!IsHumanPointer(humanPointer) ||
        myBus == nullptr ||
        fixDriver == nullptr ||
        renderMe == nullptr ||
        inWorld == nullptr)
    {
        return 0;
    }

    const auto base = static_cast<std::uintptr_t>(humanPointer);
    if (!IsReadableRange(base + HumanMyBusOffset, sizeof(int)) ||
        !IsReadableRange(base + HumanFixDriverOffset, sizeof(unsigned char)) ||
        !IsReadableRange(base + HumanRenderMeOffset, sizeof(unsigned char)) ||
        !IsReadableRange(base + HumanInWorldOffset, sizeof(unsigned char)))
    {
        return 0;
    }

    *myBus = *reinterpret_cast<const int*>(base + HumanMyBusOffset);
    *fixDriver = *reinterpret_cast<const unsigned char*>(base + HumanFixDriverOffset);
    *renderMe = *reinterpret_cast<const unsigned char*>(base + HumanRenderMeOffset);
    *inWorld = *reinterpret_cast<const unsigned char*>(base + HumanInWorldOffset);
    return 1;
}

extern "C" __declspec(dllexport) int __cdecl NavBR_DetachHumanForRoleplay(int humanPointer)
{
    if (!IsHumanPointer(humanPointer))
    {
        return 0;
    }

    const int zeroBus = 0;
    const unsigned char zero = 0;
    const unsigned char one = 1;

    return WriteValue(humanPointer, HumanMyBusOffset, zeroBus) &&
           WriteByte(humanPointer, HumanFixDriverOffset, zero) &&
           WriteByte(humanPointer, HumanRenderMeOffset, one) &&
           WriteByte(humanPointer, HumanInWorldOffset, one)
        ? 1
        : 0;
}

extern "C" __declspec(dllexport) int __cdecl NavBR_RestoreHumanDriverState(
    int humanPointer,
    int myBus,
    unsigned char fixDriver,
    unsigned char renderMe,
    unsigned char inWorld)
{
    if (!IsHumanPointer(humanPointer))
    {
        return 0;
    }

    if (myBus != 0 && !IsRoadVehiclePointer(myBus))
    {
        // Never restore a stale vehicle pointer after the bus has despawned.
        myBus = 0;
        fixDriver = 0;
    }

    return WriteValue(humanPointer, HumanMyBusOffset, myBus) &&
           WriteByte(humanPointer, HumanFixDriverOffset, fixDriver) &&
           WriteByte(humanPointer, HumanRenderMeOffset, renderMe) &&
           WriteByte(humanPointer, HumanInWorldOffset, inWorld)
        ? 1
        : 0;
}

extern "C" __declspec(dllexport) int __cdecl NavBR_ReadHumanAiState(
    int humanPointer,
    unsigned char* aiMode,
    unsigned char* aiModeEx,
    unsigned char* aiSubMode,
    float* sollSpeed,
    float* actSpeed)
{
    if (!IsHumanPointer(humanPointer) ||
        aiMode == nullptr ||
        aiModeEx == nullptr ||
        aiSubMode == nullptr ||
        sollSpeed == nullptr ||
        actSpeed == nullptr)
    {
        return 0;
    }

    const auto base = static_cast<std::uintptr_t>(humanPointer);
    if (!IsReadableRange(base + HumanAiModeOffset, sizeof(unsigned char)) ||
        !IsReadableRange(base + HumanAiModeExOffset, sizeof(unsigned char)) ||
        !IsReadableRange(base + HumanAiSubModeOffset, sizeof(unsigned char)) ||
        !IsReadableRange(base + HumanSollSpeedOffset, sizeof(float)) ||
        !IsReadableRange(base + HumanActSpeedOffset, sizeof(float)))
    {
        return 0;
    }

    *aiMode = *reinterpret_cast<const unsigned char*>(base + HumanAiModeOffset);
    *aiModeEx = *reinterpret_cast<const unsigned char*>(base + HumanAiModeExOffset);
    *aiSubMode = *reinterpret_cast<const unsigned char*>(base + HumanAiSubModeOffset);
    *sollSpeed = *reinterpret_cast<const float*>(base + HumanSollSpeedOffset);
    *actSpeed = *reinterpret_cast<const float*>(base + HumanActSpeedOffset);
    return std::isfinite(*sollSpeed) && std::isfinite(*actSpeed) ? 1 : 0;
}

extern "C" __declspec(dllexport) int __cdecl NavBR_SetHumanTransform(
    int humanPointer,
    float x,
    float y,
    float z,
    float headingDegrees,
    float speedMps)
{
    if (!IsHumanControllable(humanPointer) ||
        !std::isfinite(x) ||
        !std::isfinite(y) ||
        !std::isfinite(z) ||
        !std::isfinite(headingDegrees) ||
        !std::isfinite(speedMps) ||
        std::fabs(x) > 100000.0f ||
        std::fabs(y) > 100000.0f ||
        std::fabs(z) > 100000.0f ||
        speedMps < 0.0f ||
        speedMps > 12.0f)
    {
        return 0;
    }

    constexpr float Pi = 3.14159265358979323846f;
    const float headingRadians = headingDegrees * Pi / 180.0f;
    const float half = headingRadians * 0.5f;
    const Vec3 position{ x, y, z };
    const Quaternion rotation{ 0.0f, 0.0f, std::sin(half), std::cos(half) };

    const unsigned char aiStop = 0;      // THAM_Stop
    const unsigned char aiDoNothing = 0; // THAME_DoNothing
    const unsigned char aiSubNone = 0;

    return WriteValue(humanPointer, PositionOffset, position) &&
           WriteValue(humanPointer, RotationOffset, rotation) &&
           WriteValue(humanPointer, LastPositionOffset, position) &&
           WriteValue(humanPointer, LastRotationOffset, rotation) &&
           WriteValue(humanPointer, HumanSollHeadingOffset, headingDegrees) &&
           WriteValue(humanPointer, HumanActHeadingOffset, headingDegrees) &&
           WriteValue(humanPointer, HumanSollSpeedOffset, speedMps) &&
           WriteValue(humanPointer, HumanActSpeedOffset, speedMps) &&
           WriteByte(humanPointer, HumanAiModeOffset, aiStop) &&
           WriteByte(humanPointer, HumanAiModeExOffset, aiDoNothing) &&
           WriteByte(humanPointer, HumanAiSubModeOffset, aiSubNone)
        ? 1
        : 0;
}

extern "C" __declspec(dllexport) int __cdecl NavBR_RestoreHumanAiState(
    int humanPointer,
    unsigned char aiMode,
    unsigned char aiModeEx,
    unsigned char aiSubMode,
    float sollSpeed,
    float actSpeed)
{
    if (!IsHumanPointer(humanPointer) ||
        !std::isfinite(sollSpeed) ||
        !std::isfinite(actSpeed))
    {
        return 0;
    }

    return WriteByte(humanPointer, HumanAiModeOffset, aiMode) &&
           WriteByte(humanPointer, HumanAiModeExOffset, aiModeEx) &&
           WriteByte(humanPointer, HumanAiSubModeOffset, aiSubMode) &&
           WriteValue(humanPointer, HumanSollSpeedOffset, sollSpeed) &&
           WriteValue(humanPointer, HumanActSpeedOffset, actSpeed)
        ? 1
        : 0;
}

extern "C" __declspec(dllexport) int __cdecl NavBR_SetVehicleTransform(
    int vehiclePointer,
    float x,
    float y,
    float z,
    float rotationX,
    float rotationY,
    float rotationZ,
    float rotationW,
    float groundSpeedMps)
{
    if (!IsRoadVehiclePointer(vehiclePointer) ||
        !std::isfinite(x) || !std::isfinite(y) || !std::isfinite(z) ||
        !std::isfinite(rotationX) || !std::isfinite(rotationY) ||
        !std::isfinite(rotationZ) || !std::isfinite(rotationW) ||
        !std::isfinite(groundSpeedMps))
    {
        return 0;
    }

    const float quaternionLength = std::sqrt(
        rotationX * rotationX +
        rotationY * rotationY +
        rotationZ * rotationZ +
        rotationW * rotationW);
    if (!std::isfinite(quaternionLength) || quaternionLength < 0.0001f)
    {
        return 0;
    }

    const float inverseLength = 1.0f / quaternionLength;
    const Vec3 position{ x, y, z };
    const Quaternion rotation{
        rotationX * inverseLength,
        rotationY * inverseLength,
        rotationZ * inverseLength,
        rotationW * inverseLength
    };

    float speed = std::fabs(groundSpeedMps);
    if (speed > 150.0f)
    {
        speed = 150.0f;
    }

    const unsigned char disabled = 0;

    return WriteValue(vehiclePointer, PositionOffset, position) &&
           WriteValue(vehiclePointer, RotationOffset, rotation) &&
           WriteValue(vehiclePointer, LastPositionOffset, position) &&
           WriteValue(vehiclePointer, LastRotationOffset, rotation) &&
           WriteValue(vehiclePointer, TachoOffset, speed) &&
           WriteValue(vehiclePointer, GroundspeedOffset, speed) &&
           WriteByte(vehiclePointer, PaiOffset, disabled)
        ? 1
        : 0;
}

extern "C" __declspec(dllexport) int __cdecl NavBR_SetVehicleVisualState(
    int vehiclePointer,
    int lightFlags,
    int turnSignal)
{
    if (!IsRoadVehiclePointer(vehiclePointer))
    {
        return 0;
    }

    const bool externalLights = (lightFlags & 0x0F) != 0;
    const bool brakeLights = (lightFlags & (1 << 4)) != 0;
    const bool interiorLights = (lightFlags & (1 << 6)) != 0;
    const bool hazard = (lightFlags & (1 << 7)) != 0 || turnSignal == 3;
    const bool left = hazard || turnSignal == 1;
    const bool right = hazard || turnSignal == 2;

    const float externalValue = externalLights ? 1.0f : 0.0f;
    const float brakeValue = brakeLights ? 1.0f : 0.0f;
    const float interiorValue = interiorLights ? 1.0f : 0.0f;
    const float leftValue = left ? 1.0f : 0.0f;
    const float rightValue = right ? 1.0f : 0.0f;
    const unsigned char disabled = 0;

    return WriteByte(vehiclePointer, PaiOffset, disabled) &&
           WriteValue(vehiclePointer, AiLightOffset, externalValue) &&
           WriteValue(vehiclePointer, AiInteriorLightOffset, interiorValue) &&
           WriteValue(vehiclePointer, AiBlinkerLeftOffset, leftValue) &&
           WriteValue(vehiclePointer, AiBlinkerRightOffset, rightValue) &&
           WriteValue(vehiclePointer, AiBrakeLightOffset, brakeValue)
        ? 1
        : 0;
}

extern "C" __declspec(dllexport) int __cdecl NavBR_MarkVehicleForKilling(int vehiclePointer)
{
    if (!IsRoadVehiclePointer(vehiclePointer))
    {
        return 0;
    }

    return WriteByte(vehiclePointer, MarkedForKillingOffset, 1) ? 1 : 0;
}
