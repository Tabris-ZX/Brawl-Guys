# Brawl Guys

一个使用 **.NET 8 + WPF** 编写的赛博逗蛐蛐游戏。(创意来源 [@SpennTF2](https://www.youtube.com/@Spenntf2/shorts))

WPF层负责显示竞技场、角色血量和测试入口 (后期准备换unity)

Core层负责战斗规则、角色配置、技能与投掷物逻辑。

## 快速启动

### 下载源码
1. 安装 .NET 8 SDK。
2. 在项目根目录执行：

```powershell README.md
dotnet run --project .\BrawlGuys.Wpf\BrawlGuys.Wpf.csproj
```
也可以直接用 Visual Studio / Rider 打开 `BrawlGuys.sln` 运行 `BrawlGuys.Wpf`。

### 发行版
暂未发布

## 项目目录

```text README.md
.
├─ BrawlGuys.sln                         # 解决方案
├─ README.md                             # 项目说明
├─ BrawlGuys.Core/                       # 战斗核心逻辑，不依赖 WPF
│  ├─ Battle/                            # 战斗世界、投掷物、特效、向量和全局调参
│  ├─ Catalogs/                          # 读取并校验角色、投掷物配置
│  ├─ Config/global.json                 # 全局配置
│  ├─ Data/roles.json                    # 角色基础配置
│  ├─ Data/throwable.json                # 旧版投掷物配置
│  ├─ Models/                            # 角色/召唤物/投掷物数据模型
│  └─ Skills/
│     ├─ IFighterSkill.cs                # 技能接口与扩展钩子
│     ├─ SkillRegistry.cs                # 自动扫描并注册角色技能
│     └─ Roles/*.cs                      # 每个角色一个技能文件
└─ BrawlGuys.Wpf/                        # WPF 表现层
   ├─ MainWindow.xaml                    # 主界面布局
   ├─ MainWindow.xaml.cs                 # UI 事件、绘制和弹窗
   ├─ TexturePathResolver.cs             # 逻辑资源路径到实际文件的映射
   └─ Resources/
      ├─ Styles/ControlStyles.xaml       # 控件样式
      └─ Images/Textures/
         ├─ Roles/                       # 角色贴图
         └─ Throwable/                   # 投掷物贴图
```

## 新增角色

新增角色一般只需要三步：加角色配置、加贴图、加技能文件。

### 1. 在 `BrawlGuys.Core/Data/roles.json` 新增角色配置

`key` 必须全局唯一，并且要和技能文件里的 `IFighterSkill.Key` 完全一致。`texturePath` 使用逻辑路径，角色贴图通常写成 `roles/xxx.png`。

```json README.md
{
  "key": "new-hero",
  "name": "新角色",
  "desc": "角色描述，会显示在右侧面板",
  "texturePath": "roles/new-hero.png",
  "radius": 50,
  "HP": 1000,
  "Speed": 160,
  "CD": 3.0,
  "Accuracy": 80,
  "IsMirror": 1,
  "ProjectileTexturePath": "throwable/baseball.png",
  "ProjectileSpeed": 430,
  "ProjectileRadius": 14,
  "ProjectileDamage": 80
}
```

**字段说明:**

- `radius`：角色碰撞箱半径(目前角色碰撞箱是正圆形)。
- `HP`：最大生命值。
- `Speed`：移动速度。
- `CD`：技能/攻击间隔，单位秒。
- `Accuracy`：命中率，范围 `0~100`((**100-命中率**)/2 为偏移角大小)
- `IsMirror`：是否根据敌人方向翻转贴图，`1` 表示翻转，`0` 表示不翻转。
- `ProjectileTexturePath`、`ProjectileSpeed`、`ProjectileRadius`、`ProjectileDamage`：角色默认投掷物配置。若技能需要普通投掷物，推荐直接写在 `roles.json` 里。

> 兼容说明：项目仍支持在 `BrawlGuys.Core/Data/throwable.json` 中按角色 `key` 配置旧版投掷物；
> 如果角色技能 `RequiresProjectileDefinition` 为 `true`，则必须在 `roles.json` 或 `throwable.json` 中提供完整投掷物配置。

### 2. 放入资源贴图

- 角色贴图放到：`BrawlGuys.Wpf/Resources/Images/Textures/Roles/`
- 投掷物贴图放到：`BrawlGuys.Wpf/Resources/Images/Textures/Throwable/`

例如 `texturePath: "roles/new-hero.png"` 对应实际文件：

```text README.md
BrawlGuys.Wpf/Resources/Images/Textures/Roles/new-hero.png
```

资源目录已经在 `BrawlGuys.Wpf.csproj` 中配置为内容文件，会自动复制到输出目录。

### 3. 新增技能文件

在 `BrawlGuys.Core/Skills/Roles/` 下新增一个类，实现 `IFighterSkill`。命名空间必须保持在 `BrawlGuys.Core.Skills.Roles` 下，这样 `SkillRegistry` 才能自动扫描到。

最简单的普通投掷物技能示例：

```csharp README.md
namespace BrawlGuys.Core.Skills.Roles;

public sealed class NewHeroSkill : IFighterSkill
{
    public string Key => "new-hero";

    public void Execute(BattleWorld world, FighterState caster, FighterState target)
    {
        world.SpawnProjectile(
            owner: caster,
            target: target);

        caster.SkillFlashTime = 0.25;
    }
}
```

如果角色不需要默认投掷物，可以覆写：

```csharp README.md
public bool RequiresProjectileDefinition => false;
```

常用技能钩子包括：

- `Execute`：技能释放时调用。
- `CanUseSkill`：决定当前是否可以释放技能。
- `OnMatchStarted`：每局开始时初始化状态。
- `ModifyOutgoingDamage` / `ModifyIncomingDamage`：修改造成/受到的伤害。
- `OnHitTarget` / `OnDamaged`：命中或受伤后的触发逻辑。
- `GetDescription`：右侧面板显示的技能描述。
- `GetArenaHintText`：角色头顶提示文字。

### 4. 检查与运行

新增后执行：

```powershell README.md
dotnet build .\BrawlGuys.sln
```

**注:** 如果 `roles.json` 中有角色缺少对应技能文件、技能 `Key` 找不到角色配置、`Accuracy` 超范围或投掷物配置不完整，启动/构建运行时会报出明确错误。

## 持续更新中...