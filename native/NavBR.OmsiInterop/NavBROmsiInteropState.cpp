#include <windows.h>
#include <algorithm>
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

    constexpr int RoadVehicleListItemsOffset = 0x28;
    constexpr int RoadVehicleListCountOffset = 0x2C;
    constexpr int ObjectListItemsPointerOffset = 0x04;
    constexpr int MaxReasonableRoadVehicles = 4096;

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

        for (var index = 0; index < count; ++index)
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
    return 1;
}

extern "C" __declspec(dllexport) int __cdecl NavBR_IsRoadVehiclePointer(int vehiclePointer)
{
    return IsRoadVehiclePointer(vehiclePointer) ? 1 : 0;
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
    const float speed = std::clamp(std::fabs(groundSpeedMps), 0.0f, 150.0f);
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
