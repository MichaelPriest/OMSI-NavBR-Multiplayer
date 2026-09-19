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
    constexpr std::uintptr_t RvaMapPointer = 0x00861588u - PreferredImageBase;
    constexpr std::uintptr_t RvaHumansPointer = 0x0086172Cu - PreferredImageBase;
    constexpr std::uintptr_t RvaPlayerVehicleIndex = 0x00861740u - PreferredImageBase;

    constexpr int RoadVehicleListItemsOffset = 0x28;
    constexpr int RoadVehicleListCountOffset = 0x2C;
    constexpr int ObjectListItemsPointerOffset = 0x04;
    constexpr int MaxReasonableRoadVehicles = 4096;
    constexpr int MaxReasonableHumans = 8192;
    constexpr int MaxReasonableMapTiles = 200000;

    constexpr int PositionOffset = 0x004;
    constexpr int PositionMatrixOffset = 0x010;
    constexpr int RotationOffset = 0x050;
    constexpr int KachelOffset = 0x074;
    constexpr int AbsolutePositionOffset = 0x078;
    constexpr int AbsolutePositionThreadFreeOffset = 0x0F8;
    constexpr int MarkedForKillingOffset = 0x25C;
    constexpr int LastPositionOffset = 0x26E;
    constexpr int LastRotationOffset = 0x27A;
    constexpr int RelativeMatrixVarOffset = 0x28A;
    constexpr int MyKachelPointOffset = 0x2CC;
    constexpr int CalcTimerOffset = 0x2D8;
    constexpr int VisibleLogicalOffset = 0x2DC;
    constexpr int VisibleLogicalRenderThreadOffset = 0x2DD;
    constexpr int TachoOffset = 0x424;
    constexpr int GroundspeedOffset = 0x428;
    constexpr int OutsideMatrixOffset = 0x44C;
    constexpr int OutsideMatrixThreadFreeOffset = 0x48C;
    constexpr int PaiOffset = 0x624;
    constexpr int AiLightOffset = 0x634;
    constexpr int AiInteriorLightOffset = 0x638;
    constexpr int AiBlinkerLeftOffset = 0x63C;
    constexpr int AiBlinkerRightOffset = 0x640;
    constexpr int AiBrakeLightOffset = 0x644;
    constexpr int RoadVehicleOnLoadedKachelOffset = 0x714;
    constexpr int RoadVehicleWasCalculatedOffset = 0x715;
    constexpr int RoadVehiclePhysicsNeedPreCalcOffset = 0x75C;

    constexpr int MapKachelLoadedOffset = 0x038;
    constexpr int MapKachelnOffset = 0x118;
    constexpr int MapKachelInfosOffset = 0x11C;
    constexpr int MapKachelInfoSize = 0x10;
    constexpr int MapKachelInfoGridXOffset = 0x00;
    constexpr int MapKachelInfoGridYOffset = 0x04;
    constexpr int MapKachelInfoTilePointerOffset = 0x0C;
    constexpr int MapLoadedOffset = 0x120;

    // OmsiHumanBeingInst offsets documented by public OMSI reverse-engineering
    // references. These are guarded by membership in the global Humans array.
    constexpr int HumanDefinitionOffset = 0x5B0;
    constexpr int HumanRenderMeOffset = 0x5BC;
    constexpr int HumanInWorldOffset = 0x5EF;
    constexpr int HumanSollHeadingOffset = 0x69C;
    constexpr int HumanSollSpeedOffset = 0x6A0;
    constexpr int HumanActSpeedOffset = 0x6A4;
    constexpr int HumanActHeadingOffset = 0x6A8;
    // Legacy human movement/animation variables exposed by OmsiHook as
    // LastMovedDist and State. Their numeric State semantics are intentionally
    // not interpreted here; diagnostics expose the raw finite value only.
    constexpr int HumanLastMovedDistOffset = 0x644;
    constexpr int HumanStateOffset = 0x64C;
    constexpr int HumanFixDriverOffset = 0x662;
    constexpr int HumanActivityLegOffset = 0x663;
    constexpr int HumanActivityArmUmbrellaOffset = 0x664;
    constexpr int HumanActivityArmKiOffset = 0x665;
    constexpr int HumanActivityHeadKiOffset = 0x666;
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

    struct Matrix4
    {
        float m00, m01, m02, m03;
        float m10, m11, m12, m13;
        float m20, m21, m22, m23;
        float m30, m31, m32, m33;
    };

    struct Point2
    {
        int x;
        int y;
    };

    Matrix4 BuildTransformMatrix(
        const Quaternion& rotation,
        const Vec3& translation)
    {
        const float xx = rotation.x * rotation.x;
        const float yy = rotation.y * rotation.y;
        const float zz = rotation.z * rotation.z;
        const float xy = rotation.x * rotation.y;
        const float xz = rotation.x * rotation.z;
        const float yz = rotation.y * rotation.z;
        const float wx = rotation.w * rotation.x;
        const float wy = rotation.w * rotation.y;
        const float wz = rotation.w * rotation.z;

        return Matrix4{
            1.0f - 2.0f * (yy + zz),
            2.0f * (xy + wz),
            2.0f * (xz - wy),
            0.0f,

            2.0f * (xy - wz),
            1.0f - 2.0f * (xx + zz),
            2.0f * (yz + wx),
            0.0f,

            2.0f * (xz + wy),
            2.0f * (yz - wx),
            1.0f - 2.0f * (xx + yy),
            0.0f,

            translation.x,
            translation.y,
            translation.z,
            1.0f
        };
    }

    bool TryQuaternionHeadingDegrees(const Quaternion& rotation, float& headingDegrees)
    {
        if (!std::isfinite(rotation.x) ||
            !std::isfinite(rotation.y) ||
            !std::isfinite(rotation.z) ||
            !std::isfinite(rotation.w))
        {
            return false;
        }

        const float length = std::sqrt(
            rotation.x * rotation.x +
            rotation.y * rotation.y +
            rotation.z * rotation.z +
            rotation.w * rotation.w);
        if (!std::isfinite(length) || length < 0.0001f)
        {
            return false;
        }

        const float inverse = 1.0f / length;
        const float x = rotation.x * inverse;
        const float y = rotation.y * inverse;
        const float z = rotation.z * inverse;
        const float w = rotation.w * inverse;
        const float sinYaw = 2.0f * (w * z + x * y);
        const float cosYaw = 1.0f - 2.0f * (y * y + z * z);
        constexpr float RadToDeg = 57.295779513082320876f;
        float value = std::atan2(sinYaw, cosYaw) * RadToDeg;
        value = std::fmod(value, 360.0f);
        if (value < 0.0f)
        {
            value += 360.0f;
        }

        headingDegrees = value;
        return std::isfinite(value);
    }

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

    bool TryGetMapTileItems(int& count, int& itemArray)
    {
        count = 0;
        itemArray = 0;

        const auto globalAddress = Resolve(RvaMapPointer);
        if (!IsReadableRange(globalAddress, sizeof(int)))
        {
            return false;
        }

        const int mapPointer = *reinterpret_cast<const int*>(globalAddress);
        if (mapPointer == 0 ||
            !IsReadableRange(
                static_cast<std::uintptr_t>(mapPointer) + MapLoadedOffset,
                sizeof(unsigned char)) ||
            !IsReadableRange(
                static_cast<std::uintptr_t>(mapPointer) + MapKachelnOffset,
                sizeof(int)))
        {
            return false;
        }

        const auto loaded = *reinterpret_cast<const unsigned char*>(
            static_cast<std::uintptr_t>(mapPointer) + MapLoadedOffset);
        if (loaded == 0)
        {
            return false;
        }

        const int items = *reinterpret_cast<const int*>(
            static_cast<std::uintptr_t>(mapPointer) + MapKachelnOffset);
        if (items == 0)
        {
            return false;
        }

        const auto lengthAddress =
            static_cast<std::uintptr_t>(items) - sizeof(int);
        if (!IsReadableRange(lengthAddress, sizeof(int)))
        {
            return false;
        }

        const int currentCount =
            *reinterpret_cast<const int*>(lengthAddress);
        if (currentCount <= 0 ||
            currentCount > MaxReasonableMapTiles ||
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

    bool TryGetMapTilePointerByIndex(
        int mapTileIndex,
        int& tilePointer)
    {
        tilePointer = 0;
        if (mapTileIndex < 0)
        {
            return false;
        }

        int count = 0;
        int items = 0;
        if (!TryGetMapTileItems(count, items) ||
            mapTileIndex >= count)
        {
            return false;
        }

        const auto itemAddress =
            static_cast<std::uintptr_t>(items) +
            static_cast<std::uintptr_t>(mapTileIndex) * sizeof(int);
        if (!IsReadableRange(itemAddress, sizeof(int)))
        {
            return false;
        }

        const int candidate =
            *reinterpret_cast<const int*>(itemAddress);
        if (candidate == 0 ||
            !IsReadableRange(
                static_cast<std::uintptr_t>(candidate) + MapKachelLoadedOffset,
                sizeof(unsigned char)) ||
            *reinterpret_cast<const unsigned char*>(
                static_cast<std::uintptr_t>(candidate) + MapKachelLoadedOffset) == 0)
        {
            return false;
        }

        tilePointer = candidate;
        return true;
    }

    bool TryGetMapTileIndexByPointer(
        int tilePointer,
        int& mapTileIndex)
    {
        mapTileIndex = -1;
        if (tilePointer <= 0)
        {
            return false;
        }

        int count = 0;
        int items = 0;
        if (!TryGetMapTileItems(count, items))
        {
            return false;
        }

        for (int index = 0; index < count; ++index)
        {
            const auto itemAddress =
                static_cast<std::uintptr_t>(items) +
                static_cast<std::uintptr_t>(index) * sizeof(int);
            if (!IsReadableRange(itemAddress, sizeof(int)))
            {
                return false;
            }

            if (*reinterpret_cast<const int*>(itemAddress) == tilePointer)
            {
                mapTileIndex = index;
                return true;
            }
        }

        return false;
    }

    bool IsMapTileIndexValid(int mapTileIndex)
    {
        int tilePointer = 0;
        return TryGetMapTilePointerByIndex(mapTileIndex, tilePointer);
    }

    bool TryResolveMapTileIndexByGrid(
        int gridX,
        int gridY,
        int& mapTileIndex)
    {
        mapTileIndex = -1;
        if (std::abs(static_cast<long long>(gridX)) > 100000LL ||
            std::abs(static_cast<long long>(gridY)) > 100000LL)
        {
            return false;
        }

        int tileCount = 0;
        int tileItems = 0;
        if (!TryGetMapTileItems(tileCount, tileItems))
        {
            return false;
        }

        const auto globalAddress = Resolve(RvaMapPointer);
        if (!IsReadableRange(globalAddress, sizeof(int)))
        {
            return false;
        }

        const int mapPointer = *reinterpret_cast<const int*>(globalAddress);
        if (mapPointer == 0 ||
            !IsReadableRange(
                static_cast<std::uintptr_t>(mapPointer) + MapKachelInfosOffset,
                sizeof(int)))
        {
            return false;
        }

        const int infos = *reinterpret_cast<const int*>(
            static_cast<std::uintptr_t>(mapPointer) + MapKachelInfosOffset);
        if (infos == 0)
        {
            return false;
        }

        const auto infoLengthAddress =
            static_cast<std::uintptr_t>(infos) - sizeof(int);
        if (!IsReadableRange(infoLengthAddress, sizeof(int)))
        {
            return false;
        }

        const int infoCount =
            *reinterpret_cast<const int*>(infoLengthAddress);
        if (infoCount <= 0 ||
            infoCount > MaxReasonableMapTiles ||
            !IsReadableRange(
                static_cast<std::uintptr_t>(infos),
                static_cast<std::size_t>(infoCount) *
                    static_cast<std::size_t>(MapKachelInfoSize)))
        {
            return false;
        }

        for (int infoIndex = 0; infoIndex < infoCount; ++infoIndex)
        {
            const auto infoAddress =
                static_cast<std::uintptr_t>(infos) +
                static_cast<std::uintptr_t>(infoIndex) *
                    static_cast<std::uintptr_t>(MapKachelInfoSize);

            const int x = *reinterpret_cast<const int*>(
                infoAddress + MapKachelInfoGridXOffset);
            const int y = *reinterpret_cast<const int*>(
                infoAddress + MapKachelInfoGridYOffset);
            if (x != gridX || y != gridY)
            {
                continue;
            }

            const int tilePointer = *reinterpret_cast<const int*>(
                infoAddress + MapKachelInfoTilePointerOffset);
            if (tilePointer == 0 ||
                !IsReadableRange(
                    static_cast<std::uintptr_t>(tilePointer) +
                        MapKachelLoadedOffset,
                    sizeof(unsigned char)) ||
                *reinterpret_cast<const unsigned char*>(
                    static_cast<std::uintptr_t>(tilePointer) +
                        MapKachelLoadedOffset) == 0)
            {
                return false;
            }

            for (int tileIndex = 0; tileIndex < tileCount; ++tileIndex)
            {
                const auto itemAddress =
                    static_cast<std::uintptr_t>(tileItems) +
                    static_cast<std::uintptr_t>(tileIndex) * sizeof(int);
                if (!IsReadableRange(itemAddress, sizeof(int)))
                {
                    return false;
                }

                const int candidate =
                    *reinterpret_cast<const int*>(itemAddress);
                if (candidate == tilePointer)
                {
                    mapTileIndex = tileIndex;
                    return true;
                }
            }

            return false;
        }

        return false;
    }


    bool TryGetMapTileGridByPointer(
        int tilePointer,
        int& gridX,
        int& gridY)
    {
        gridX = 0;
        gridY = 0;
        if (tilePointer <= 0)
        {
            return false;
        }

        const auto globalAddress = Resolve(RvaMapPointer);
        if (!IsReadableRange(globalAddress, sizeof(int)))
        {
            return false;
        }

        const int mapPointer = *reinterpret_cast<const int*>(globalAddress);
        if (mapPointer == 0 ||
            !IsReadableRange(
                static_cast<std::uintptr_t>(mapPointer) + MapKachelInfosOffset,
                sizeof(int)))
        {
            return false;
        }

        const int infos = *reinterpret_cast<const int*>(
            static_cast<std::uintptr_t>(mapPointer) + MapKachelInfosOffset);
        if (infos == 0)
        {
            return false;
        }

        const auto infoLengthAddress =
            static_cast<std::uintptr_t>(infos) - sizeof(int);
        if (!IsReadableRange(infoLengthAddress, sizeof(int)))
        {
            return false;
        }

        const int infoCount =
            *reinterpret_cast<const int*>(infoLengthAddress);
        if (infoCount <= 0 ||
            infoCount > MaxReasonableMapTiles ||
            !IsReadableRange(
                static_cast<std::uintptr_t>(infos),
                static_cast<std::size_t>(infoCount) *
                    static_cast<std::size_t>(MapKachelInfoSize)))
        {
            return false;
        }

        for (int infoIndex = 0; infoIndex < infoCount; ++infoIndex)
        {
            const auto infoAddress =
                static_cast<std::uintptr_t>(infos) +
                static_cast<std::uintptr_t>(infoIndex) *
                    static_cast<std::uintptr_t>(MapKachelInfoSize);
            const int candidate = *reinterpret_cast<const int*>(
                infoAddress + MapKachelInfoTilePointerOffset);
            if (candidate != tilePointer)
            {
                continue;
            }

            const int x = *reinterpret_cast<const int*>(
                infoAddress + MapKachelInfoGridXOffset);
            const int y = *reinterpret_cast<const int*>(
                infoAddress + MapKachelInfoGridYOffset);
            if (std::abs(static_cast<long long>(x)) > 100000LL ||
                std::abs(static_cast<long long>(y)) > 100000LL)
            {
                return false;
            }

            gridX = x;
            gridY = y;
            return true;
        }

        return false;
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
        if (!IsHumanPointer(humanPointer) || definitionPointer < 0)
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
        if (myBus != playerVehicle)
        {
            return false;
        }

        // Always require the live seated-driver state. Matching only the
        // definition pointer is not sufficient because passengers can reuse
        // the same human definition as the driver on some maps/add-ons.
        const auto aiModeEx = *reinterpret_cast<const unsigned char*>(
            base + HumanAiModeExOffset);
        const auto fixDriver = *reinterpret_cast<const unsigned char*>(
            base + HumanFixDriverOffset);
        if (aiModeEx != 9 && fixDriver == 0)
        {
            return false;
        }

        // An explicit catalog selection additionally keeps exact-definition
        // matching. definitionPointer == 0 means "resolve the live driver".
        return definitionPointer <= 0 ||
               humanDefinition == definitionPointer;
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
            !IsReadableRange(base + RotationOffset, sizeof(Quaternion)) ||
            !IsReadableRange(base + HumanActSpeedOffset, sizeof(float)))
        {
            return false;
        }

        const auto position = *reinterpret_cast<const Vec3*>(base + PositionOffset);
        const auto rotation = *reinterpret_cast<const Quaternion*>(base + RotationOffset);
        const float currentSpeed = *reinterpret_cast<const float*>(base + HumanActSpeedOffset);
        float currentHeading = 0.0f;
        if (!std::isfinite(position.x) ||
            !std::isfinite(position.y) ||
            !std::isfinite(position.z) ||
            !std::isfinite(currentSpeed) ||
            !TryQuaternionHeadingDegrees(rotation, currentHeading))
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

    template <typename T>
    bool WriteValue(int vehiclePointer, int offset, const T& value);

    bool TryReadMatrix(int objectPointer, int offset, Matrix4& matrix)
    {
        const auto address =
            static_cast<std::uintptr_t>(objectPointer) +
            static_cast<std::uintptr_t>(offset);
        if (!IsReadableRange(address, sizeof(Matrix4)))
        {
            return false;
        }

        matrix = *reinterpret_cast<const Matrix4*>(address);
        return
            std::isfinite(matrix.m30) &&
            std::isfinite(matrix.m31) &&
            std::isfinite(matrix.m32);
    }

    bool TryResolveWorldTranslation(
        int objectPointer,
        int targetTilePointer,
        const Vec3& targetPosition,
        Vec3& worldPosition)
    {
        worldPosition = targetPosition;

        // Prefer the player's bus as the render-space origin when it is on the
        // same Kachel. This preserves OMSI's current tile/center offset without
        // guessing the map's absolute coordinate convention.
        const int playerVehicle = GetPlayerVehiclePointer();
        if (IsRoadVehiclePointer(playerVehicle))
        {
            const auto playerBase =
                static_cast<std::uintptr_t>(playerVehicle);
            if (IsReadableRange(playerBase + KachelOffset, sizeof(int)) &&
                *reinterpret_cast<const int*>(playerBase + KachelOffset) ==
                    targetTilePointer &&
                IsReadableRange(playerBase + PositionOffset, sizeof(Vec3)))
            {
                const auto playerPosition =
                    *reinterpret_cast<const Vec3*>(
                        playerBase + PositionOffset);
                Matrix4 playerAbsolute{};
                if (TryReadMatrix(
                        playerVehicle,
                        AbsolutePositionOffset,
                        playerAbsolute))
                {
                    worldPosition.x =
                        targetPosition.x +
                        (playerAbsolute.m30 - playerPosition.x);
                    worldPosition.y =
                        targetPosition.y +
                        (playerAbsolute.m31 - playerPosition.y);
                    worldPosition.z =
                        targetPosition.z +
                        (playerAbsolute.m32 - playerPosition.z);
                    return true;
                }
            }
        }

        // Once an owned object is already on the target Kachel, retain its
        // existing OMSI render-space origin and only change the local delta.
        const auto base =
            static_cast<std::uintptr_t>(objectPointer);
        if (IsReadableRange(base + KachelOffset, sizeof(int)) &&
            *reinterpret_cast<const int*>(base + KachelOffset) ==
                targetTilePointer &&
            IsReadableRange(base + PositionOffset, sizeof(Vec3)))
        {
            const auto previousPosition =
                *reinterpret_cast<const Vec3*>(base + PositionOffset);
            Matrix4 previousAbsolute{};
            if (TryReadMatrix(
                    objectPointer,
                    AbsolutePositionOffset,
                    previousAbsolute))
            {
                worldPosition.x =
                    targetPosition.x +
                    (previousAbsolute.m30 - previousPosition.x);
                worldPosition.y =
                    targetPosition.y +
                    (previousAbsolute.m31 - previousPosition.y);
                worldPosition.z =
                    targetPosition.z +
                    (previousAbsolute.m32 - previousPosition.z);
                return true;
            }
        }

        return false;
    }

    bool WriteRenderMatrices(
        int objectPointer,
        const Vec3& localPosition,
        const Vec3& worldPosition,
        const Quaternion& rotation)
    {
        const Matrix4 localMatrix =
            BuildTransformMatrix(rotation, localPosition);
        const Matrix4 worldMatrix =
            BuildTransformMatrix(rotation, worldPosition);

        return
            WriteValue(
                objectPointer,
                PositionMatrixOffset,
                localMatrix) &&
            WriteValue(
                objectPointer,
                AbsolutePositionOffset,
                worldMatrix) &&
            WriteValue(
                objectPointer,
                AbsolutePositionThreadFreeOffset,
                worldMatrix) &&
            WriteValue(
                objectPointer,
                RelativeMatrixVarOffset,
                localMatrix) &&
            WriteValue(
                objectPointer,
                OutsideMatrixOffset,
                worldMatrix) &&
            WriteValue(
                objectPointer,
                OutsideMatrixThreadFreeOffset,
                worldMatrix);
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
    return 9;
}

extern "C" __declspec(dllexport) int __cdecl NavBR_ProbeRoleplayHumanControl()
{
    const auto imageBase = ImageBase();
    if (imageBase == 0 || sizeof(void*) != 4)
    {
        return 0;
    }

    // RP only needs the human list, the player/road-vehicle identity used to
    // resolve the active driver, and the state-layout ABI implemented here.
    // Do not couple character control to MakeVehicle/map-tile spawn symbols.
    return IsReadableRange(Resolve(RvaHumansPointer), sizeof(int)) &&
           IsReadableRange(Resolve(RvaRoadVehiclesPointer), sizeof(int)) &&
           IsReadableRange(Resolve(RvaPlayerVehicleIndex), sizeof(int))
        ? 1
        : 0;
}

extern "C" __declspec(dllexport) int __cdecl NavBR_IsRoadVehiclePointer(int vehiclePointer)
{
    return IsRoadVehiclePointer(vehiclePointer) ? 1 : 0;
}

extern "C" __declspec(dllexport) int __cdecl NavBR_IsMapTileIndexValid(int mapTileIndex)
{
    return IsMapTileIndexValid(mapTileIndex) ? 1 : 0;
}

extern "C" __declspec(dllexport) int __cdecl NavBR_ResolveMapTileIndex(
    int gridX,
    int gridY)
{
    int mapTileIndex = -1;
    return TryResolveMapTileIndexByGrid(gridX, gridY, mapTileIndex)
        ? mapTileIndex
        : -1;
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

extern "C" __declspec(dllexport) int __cdecl NavBR_ReadPlayerVehicleGrid(
    int* gridX,
    int* gridY,
    int* mapTileIndex)
{
    if (gridX == nullptr || gridY == nullptr || mapTileIndex == nullptr)
    {
        return 0;
    }

    const int playerVehicle = GetPlayerVehiclePointer();
    if (!IsRoadVehiclePointer(playerVehicle))
    {
        return 0;
    }

    const auto tileAddress =
        static_cast<std::uintptr_t>(playerVehicle) + KachelOffset;
    if (!IsReadableRange(tileAddress, sizeof(int)))
    {
        return 0;
    }

    const int tilePointer =
        *reinterpret_cast<const int*>(tileAddress);
    int resolvedGridX = 0;
    int resolvedGridY = 0;
    if (!TryGetMapTileGridByPointer(
            tilePointer,
            resolvedGridX,
            resolvedGridY))
    {
        return 0;
    }

    int resolvedTileIndex = -1;
    (void)TryGetMapTileIndexByPointer(tilePointer, resolvedTileIndex);

    *gridX = resolvedGridX;
    *gridY = resolvedGridY;
    *mapTileIndex = resolvedTileIndex;
    return 1;
}

extern "C" __declspec(dllexport) int __cdecl NavBR_ReadRoadVehicleTileIndex(
    int vehiclePointer)
{
    if (!IsRoadVehiclePointer(vehiclePointer))
    {
        return -1;
    }

    const auto tileAddress =
        static_cast<std::uintptr_t>(vehiclePointer) + KachelOffset;
    if (!IsReadableRange(tileAddress, sizeof(int)))
    {
        return -1;
    }

    const int tilePointer =
        *reinterpret_cast<const int*>(tileAddress);
    int tileIndex = -1;
    return TryGetMapTileIndexByPointer(tilePointer, tileIndex)
        ? tileIndex
        : -1;
}

extern "C" __declspec(dllexport) int __cdecl NavBR_ReadRoadVehiclePosition(
    int vehiclePointer,
    float* x,
    float* y,
    float* z)
{
    if (!IsRoadVehiclePointer(vehiclePointer) ||
        x == nullptr ||
        y == nullptr ||
        z == nullptr)
    {
        return 0;
    }

    const auto base = static_cast<std::uintptr_t>(vehiclePointer);
    if (!IsReadableRange(base + PositionOffset, sizeof(Vec3)))
    {
        return 0;
    }

    const auto position = *reinterpret_cast<const Vec3*>(base + PositionOffset);
    if (!std::isfinite(position.x) ||
        !std::isfinite(position.y) ||
        !std::isfinite(position.z))
    {
        return 0;
    }

    *x = position.x;
    *y = position.y;
    *z = position.z;
    return 1;
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
    const unsigned int zeroTimer = 0;
    const unsigned char zero = 0;
    const unsigned char one = 1;

    int roleplayTilePointer = 0;
    Point2 roleplayTilePoint{};
    const int playerVehicle = GetPlayerVehiclePointer();
    if (IsRoadVehiclePointer(playerVehicle))
    {
        const auto playerBase =
            static_cast<std::uintptr_t>(playerVehicle);
        if (IsReadableRange(playerBase + KachelOffset, sizeof(int)))
        {
            roleplayTilePointer =
                *reinterpret_cast<const int*>(
                    playerBase + KachelOffset);
            int roleplayGridX = 0;
            int roleplayGridY = 0;
            if (roleplayTilePointer != 0 &&
                TryGetMapTileGridByPointer(
                    roleplayTilePointer,
                    roleplayGridX,
                    roleplayGridY))
            {
                roleplayTilePoint =
                    Point2{ roleplayGridX, roleplayGridY };
            }
        }
    }

    Vec3 humanWorldPosition = position;
    if (roleplayTilePointer != 0)
    {
        (void)TryResolveWorldTranslation(
            humanPointer,
            roleplayTilePointer,
            position,
            humanWorldPosition);
    }

    return WriteValue(humanPointer, HumanMyBusOffset, zeroBus) &&
           WriteByte(humanPointer, HumanFixDriverOffset, zero) &&
           WriteByte(humanPointer, HumanRenderMeOffset, one) &&
           WriteByte(humanPointer, HumanInWorldOffset, one) &&
           WriteValue(humanPointer, CalcTimerOffset, zeroTimer) &&
           WriteByte(humanPointer, VisibleLogicalOffset, one) &&
           WriteByte(humanPointer, VisibleLogicalRenderThreadOffset, one)
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

extern "C" __declspec(dllexport) int __cdecl NavBR_ReadHumanAnimationState(
    int humanPointer,
    float* lastMovedDist,
    float* state)
{
    if (!IsHumanPointer(humanPointer) ||
        lastMovedDist == nullptr ||
        state == nullptr)
    {
        return 0;
    }

    const auto base = static_cast<std::uintptr_t>(humanPointer);
    if (!IsReadableRange(base + HumanLastMovedDistOffset, sizeof(float)) ||
        !IsReadableRange(base + HumanStateOffset, sizeof(float)))
    {
        return 0;
    }

    *lastMovedDist =
        *reinterpret_cast<const float*>(base + HumanLastMovedDistOffset);
    *state =
        *reinterpret_cast<const float*>(base + HumanStateOffset);
    return std::isfinite(*lastMovedDist) && std::isfinite(*state) ? 1 : 0;
}

extern "C" __declspec(dllexport) int __cdecl NavBR_ReadHumanActivityState(
    int humanPointer,
    unsigned char* activityLeg,
    unsigned char* activityArmUmbrella,
    unsigned char* activityArmKi,
    unsigned char* activityHeadKi)
{
    if (!IsHumanPointer(humanPointer) ||
        activityLeg == nullptr ||
        activityArmUmbrella == nullptr ||
        activityArmKi == nullptr ||
        activityHeadKi == nullptr)
    {
        return 0;
    }

    const auto base = static_cast<std::uintptr_t>(humanPointer);
    if (!IsReadableRange(base + HumanActivityLegOffset, sizeof(unsigned char)) ||
        !IsReadableRange(base + HumanActivityArmUmbrellaOffset, sizeof(unsigned char)) ||
        !IsReadableRange(base + HumanActivityArmKiOffset, sizeof(unsigned char)) ||
        !IsReadableRange(base + HumanActivityHeadKiOffset, sizeof(unsigned char)))
    {
        return 0;
    }

    *activityLeg =
        *reinterpret_cast<const unsigned char*>(base + HumanActivityLegOffset);
    *activityArmUmbrella =
        *reinterpret_cast<const unsigned char*>(base + HumanActivityArmUmbrellaOffset);
    *activityArmKi =
        *reinterpret_cast<const unsigned char*>(base + HumanActivityArmKiOffset);
    *activityHeadKi =
        *reinterpret_cast<const unsigned char*>(base + HumanActivityHeadKiOffset);
    return 1;
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

    const auto base = static_cast<std::uintptr_t>(humanPointer);
    if (!IsReadableRange(base + PositionOffset, sizeof(Vec3)))
    {
        return 0;
    }

    const auto previousPosition = *reinterpret_cast<const Vec3*>(base + PositionOffset);
    const float movedX = x - previousPosition.x;
    const float movedY = y - previousPosition.y;
    const float movedZ = z - previousPosition.z;
    const float movedDistance = std::sqrt(
        movedX * movedX +
        movedY * movedY +
        movedZ * movedZ);
    if (!std::isfinite(movedDistance))
    {
        return 0;
    }

    // Normal RP frames are sub-metre. Do not turn a guarded teleport (for
    // example returning to the bus) into a huge legacy walk-animation step.
    const bool locomotionFrame =
        speedMps > 0.01f &&
        movedDistance > 0.0001f &&
        movedDistance <= 1.0f;
    const float lastMovedDist = locomotionFrame ? movedDistance : 0.0f;
    const float paxState = locomotionFrame ? 1.0f : 0.0f;

    constexpr float Pi = 3.14159265358979323846f;
    const float headingRadians = headingDegrees * Pi / 180.0f;
    const float half = headingRadians * 0.5f;
    const Vec3 position{ x, y, z };
    const Quaternion rotation{ 0.0f, 0.0f, std::sin(half), std::cos(half) };

    const int zeroBus = 0;
    const unsigned int zeroTimer = 0;
    const unsigned char zero = 0;
    const unsigned char one = 1;
    const unsigned char aiStop = 0;      // THAM_Stop
    const unsigned char aiDoNothing = 0; // THAME_DoNothing
    const unsigned char aiSubNone = 0;

    return WriteValue(humanPointer, HumanMyBusOffset, zeroBus) &&
           WriteByte(humanPointer, HumanFixDriverOffset, zero) &&
           WriteByte(humanPointer, HumanRenderMeOffset, one) &&
           WriteByte(humanPointer, HumanInWorldOffset, one) &&
           (roleplayTilePointer == 0 ||
            WriteValue(humanPointer, KachelOffset, roleplayTilePointer)) &&
           (roleplayTilePointer == 0 ||
            WriteValue(humanPointer, MyKachelPointOffset, roleplayTilePoint)) &&
           WriteValue(humanPointer, PositionOffset, position) &&
           WriteValue(humanPointer, RotationOffset, rotation) &&
           WriteValue(humanPointer, LastPositionOffset, position) &&
           WriteValue(humanPointer, LastRotationOffset, rotation) &&
           WriteRenderMatrices(
               humanPointer,
               position,
               humanWorldPosition,
               rotation) &&
           WriteValue(humanPointer, CalcTimerOffset, zeroTimer) &&
           WriteByte(humanPointer, VisibleLogicalOffset, one) &&
           WriteByte(humanPointer, VisibleLogicalRenderThreadOffset, one) &&
           WriteValue(humanPointer, HumanLastMovedDistOffset, lastMovedDist) &&
           WriteValue(humanPointer, HumanStateOffset, paxState) &&
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
    float groundSpeedMps,
    int mapTileIndex)
{
    if (!IsRoadVehiclePointer(vehiclePointer) ||
        !std::isfinite(x) || !std::isfinite(y) || !std::isfinite(z) ||
        !std::isfinite(rotationX) || !std::isfinite(rotationY) ||
        !std::isfinite(rotationZ) || !std::isfinite(rotationW) ||
        !std::isfinite(groundSpeedMps) ||
        mapTileIndex < -1)
    {
        return 0;
    }

    int mapTilePointer = 0;
    if (mapTileIndex >= 0 &&
        (!TryGetMapTilePointerByIndex(mapTileIndex, mapTilePointer) ||
         !IsWritableRange(
             static_cast<std::uintptr_t>(vehiclePointer) + KachelOffset,
             sizeof(int))))
    {
        return 0;
    }

    int effectiveTilePointer = mapTilePointer;
    if (effectiveTilePointer == 0)
    {
        const auto vehicleBase =
            static_cast<std::uintptr_t>(vehiclePointer);
        if (IsReadableRange(vehicleBase + KachelOffset, sizeof(int)))
        {
            effectiveTilePointer =
                *reinterpret_cast<const int*>(
                    vehicleBase + KachelOffset);
        }
    }

    Point2 effectiveTilePoint{};
    int effectiveGridX = 0;
    int effectiveGridY = 0;
    const bool hasEffectiveTileGrid =
        effectiveTilePointer != 0 &&
        TryGetMapTileGridByPointer(
            effectiveTilePointer,
            effectiveGridX,
            effectiveGridY);
    if (hasEffectiveTileGrid)
    {
        effectiveTilePoint =
            Point2{ effectiveGridX, effectiveGridY };
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

    float speedMps = std::fabs(groundSpeedMps);
    if (speedMps > 150.0f)
    {
        speedMps = 150.0f;
    }

    // OMSI's Tacho/script-facing speed is km/h, while Groundspeed is m/s.
    // Keep both fields in their native units so scripts and physical motion
    // observe the same real vehicle speed.
    const float tachoKph = speedMps * 3.6f;
    const unsigned int zeroTimer = 0;
    const unsigned char disabled = 0;
    const unsigned char enabled = 1;

    Vec3 worldPosition = position;
    if (effectiveTilePointer != 0)
    {
        (void)TryResolveWorldTranslation(
            vehiclePointer,
            effectiveTilePointer,
            position,
            worldPosition);
    }

    return WriteByte(vehiclePointer, MarkedForKillingOffset, disabled) &&
           WriteValue(vehiclePointer, PositionOffset, position) &&
           WriteValue(vehiclePointer, RotationOffset, rotation) &&
           WriteValue(vehiclePointer, LastPositionOffset, position) &&
           WriteValue(vehiclePointer, LastRotationOffset, rotation) &&
           WriteRenderMatrices(
               vehiclePointer,
               position,
               worldPosition,
               rotation) &&
           (effectiveTilePointer == 0 ||
            WriteValue(
                vehiclePointer,
                KachelOffset,
                effectiveTilePointer)) &&
           (!hasEffectiveTileGrid ||
            WriteValue(
                vehiclePointer,
                MyKachelPointOffset,
                effectiveTilePoint)) &&
           WriteValue(vehiclePointer, CalcTimerOffset, zeroTimer) &&
           WriteByte(vehiclePointer, VisibleLogicalOffset, enabled) &&
           WriteByte(vehiclePointer, VisibleLogicalRenderThreadOffset, enabled) &&
           WriteValue(vehiclePointer, TachoOffset, tachoKph) &&
           WriteValue(vehiclePointer, GroundspeedOffset, speedMps) &&
           WriteByte(vehiclePointer, PaiOffset, disabled) &&
           (mapTileIndex < 0 ||
            (WriteValue(vehiclePointer, KachelOffset, mapTilePointer) &&
             WriteByte(vehiclePointer, RoadVehicleOnLoadedKachelOffset, enabled) &&
             WriteByte(vehiclePointer, RoadVehicleWasCalculatedOffset, disabled) &&
             WriteByte(vehiclePointer, RoadVehiclePhysicsNeedPreCalcOffset, enabled)))
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
