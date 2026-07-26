using System.Collections.ObjectModel;

namespace Bomber.Core;

public sealed record PowerUpDefinition(
    string Id,
    PowerUpKind Kind,
    string Name,
    string Color,
    string Description,
    int Weight);

/// <summary>The stable, UI-ready catalog and weighted drop table for every collectible chip.</summary>
public static class PowerUpCatalog
{
    private static readonly ReadOnlyCollection<PowerUpDefinition> Definitions = Array.AsReadOnly(
    new PowerUpDefinition[]
    {
        new("bomb", PowerUpKind.BombCapacity, "爆彈袋", "#ffdb54", "可同時多放一枚爆彈", 30),
        new("fire", PowerUpKind.FireRange, "烈焰核心", "#ff653d", "爆炸範圍增加一格", 30),
        new("speed", PowerUpKind.Speed, "疾風輪", "#5dffb0", "移動速度永久提升", 10),
        new("kick", PowerUpKind.Kick, "戰靴", "#50b9ff", "推動並踢飛碰到的爆彈", 7),
        new("glove", PowerUpKind.Glove, "重力拳套", "#d98cff", "技能鍵拋出前方爆彈", 5),
        new("remote", PowerUpKind.Remote, "遙控器", "#ff4ba7", "技能鍵引爆最早的自家爆彈", 5),
        new("disguise", PowerUpKind.BrickDisguise, "擬態模組", "#c247ff", "本回合爆彈偽裝成能量箱，爆風虛線仍會示警", 4),
        new("pierce", PowerUpKind.Pierce, "電漿針", "#d7fbff", "烈焰可貫穿一個能量箱", 4),
        new("bombpass", PowerUpKind.BombPass, "虛相靴", "#aa91ff", "可以穿過靜止爆彈", 4),
        new("wallpass", PowerUpKind.WallPass, "量子鑽", "#cb7dff", "可以穿過能量箱", 3),
        new("flamepass", PowerUpKind.FlamePass, "鳳凰甲", "#ffac4d", "永久免疫自己的爆彈烈焰", 2),
        new("shield", PowerUpKind.Shield, "光子護盾", "#62dfff", "抵擋下一次傷害", 8),
        new("heart", PowerUpKind.Heart, "生命晶核", "#ff6585", "增加一顆生命，最多三顆", 5),
        new("dash", PowerUpKind.Dash, "脈衝引擎", "#7dfffb", "技能鍵高速衝刺，可充能", 5),
        new("mega", PowerUpKind.Mega, "超新星", "#ffcf55", "下一枚爆彈巨大且威力加倍", 4),
        new("cluster", PowerUpKind.Cluster, "蜂群核心", "#8aff63", "下一枚爆彈散射額外火花", 4),
        new("freeze", PowerUpKind.Freeze, "零度脈衝", "#80d5ff", "立刻冰凍所有對手片刻", 3),
        new("magnet", PowerUpKind.Magnet, "磁力場", "#ff79e2", "約 2.5 格內的晶片會自動飛向你", 4),
        new("mystery", PowerUpKind.Mystery, "混沌禮盒", "#f8f8ff", "隨機神力、瞬移、反向或減速", 6)
    });

    private static readonly IReadOnlyDictionary<string, PowerUpDefinition> DefinitionsById =
        new ReadOnlyDictionary<string, PowerUpDefinition>(Definitions.ToDictionary(item => item.Id, StringComparer.Ordinal));

    private static readonly IReadOnlyDictionary<PowerUpKind, PowerUpDefinition> DefinitionsByKind =
        new ReadOnlyDictionary<PowerUpKind, PowerUpDefinition>(Definitions.ToDictionary(item => item.Kind));

    public static IReadOnlyList<PowerUpDefinition> All => Definitions;
    public static int TotalWeight { get; } = Definitions.Sum(item => item.Weight);

    public static PowerUpDefinition Get(string id) =>
        DefinitionsById.TryGetValue(id, out var definition)
            ? definition
            : throw new KeyNotFoundException($"Unknown power-up id '{id}'.");

    public static PowerUpDefinition Get(PowerUpKind kind) => DefinitionsByKind[kind];

    internal static PowerUpDefinition Select(DeterministicRandom random)
    {
        var roll = random.NextInt(TotalWeight);
        foreach (var definition in Definitions)
        {
            if (roll < definition.Weight)
            {
                return definition;
            }

            roll -= definition.Weight;
        }

        throw new InvalidOperationException("The weighted power-up table is invalid.");
    }
}
