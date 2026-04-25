\# TopDownShooterDemo



一个使用 Unity / Tuanjie 制作的 2D 俯视角射击 playable vertical slice。



本项目用于客户端开发求职作品展示，重点展示：



\- 选角流程

\- 2D 俯视角射击战斗

\- 三角色主动技能

\- 双武器切换与武器台替换

\- 小怪波次与 Gate 推进

\- Boss 战

\- 第一关通关后传送至第二关

\- HUD、伤害数字、血条、FX、音效反馈

\- 清晰的 runtime ownership 和 display-only 表现层边界



当前项目不是完整商业游戏，而是一个可运行、可展示、可继续扩展的 vertical slice。



\---



\## 快速试玩 / HR 测试指南



如果只是想快速了解项目效果，建议优先查看 Demo 视频和截图。  

如果需要亲自运行项目，可以按下面步骤测试完整流程。



\### 推荐测试入口



推荐从下面这个场景开始运行：



Assets/Scenes/CharacterSelectScene.scene



这是当前项目的正式入口，可以完整体验：



选角 -> 第一关 -> Boss -> 下一关传送门 -> 第二关



\### 编辑器中如何运行



1\. 使用 Unity / Tuanjie 打开项目根目录。

2\. 打开场景：Assets/Scenes/CharacterSelectScene.scene

3\. 点击 Play。

4\. 在选角页面选择一个角色。

5\. 点击“确认选择”。

6\. 使用 WASD 控制角色走到传送门。

7\. 在传送门处按 E 进入第一关。



\### 操作按键



移动：WASD  

瞄准：鼠标位置  

射击：鼠标左键  

切换武器：Q  

通用闪避：Space  

角色主动技能：F  

交互 / 传送门 / 武器台：E  

暂停：Escape  

Result 面板返回：Enter / Space / 按钮



\### 推荐试玩路线



建议按下面路线测试，可以在几分钟内体验主要功能：



1\. 在选角房间选择任意角色。

2\. 点击“确认选择”。

3\. 使用 WASD 控制角色走到传送门处。

4\. 按 E 进入第一关。

5\. 在第一关起始房间测试移动、闪避、技能、切枪和射击。

6\. 靠近左侧或右侧武器台，按 E 替换当前武器。

7\. 进入小怪房，击败小怪波次。

8\. Gate 打开后进入 Boss 房。

9\. 击败 Boss。

10\. Boss 房中间出现下一关传送门。

11\. 靠近传送门，看到提示“按 E 进入第二关”。

12\. 按 E 进入 RunScene\_Level2。

13\. 在第二关确认玩家可以正常移动、闪避、切枪、射击。



\### 正常完成时应该看到



完整流程正常时，应该能看到：



\- 中文选角界面

\- 三名角色可选择

\- 第一关中小怪波次和 Boss 战

\- 伤害数字、小怪血条、枪口火光、命中特效、死亡特效

\- 起始房间两个可交互武器台

\- Boss 死亡后出现下一关传送门

\- 传送门提示“按 E 进入第二关”

\- 成功进入第二关

\- 第二关中玩家、HUD、Camera、边界均正常



\### 快速查看效果



如果不方便打开 Unity / Tuanjie，可以查看：



Demo 视频：待补充  

截图目录：Docs/Screenshots/  

Build 下载：待补充



建议 Demo 视频展示顺序：



选角 -> 第一关战斗 -> 武器台 -> Boss -> 下一关传送门 -> 第二关



\---



\## 项目概览



当前版本已经打通完整演示流程：



CharacterSelectScene  

\-> 选择角色  

\-> 确认角色  

\-> 通过传送门进入 RunScene  

\-> 第一关小怪波次  

\-> Boss 战  

\-> Boss 死亡后出现下一关传送门  

\-> 按 E 进入 RunScene\_Level2  

\-> 第二关最小可运行场景



当前项目定位：



Unity / Tuanjie 2D Top-down Shooter Playable Vertical Slice



适合作为客户端开发求职作品，用于展示 gameplay 主链路、架构边界、表现层接入和场景扩展能力。



\---



\## 当前完整玩法流程



1\. 打开 CharacterSelectScene。

2\. 选择三名角色之一：游侠、守卫、特勤。

3\. 点击“确认选择”。

4\. 使用 WASD 控制已确认角色。

5\. 在选角房间传送门处按 E 进入第一关。

6\. 第一关中可以移动、翻滚、射击、切换武器、使用角色主动技能。

7\. 在起始房间通过武器台替换当前武器槽。

8\. 清理小怪波次。

9\. 进入 Boss 房。

10\. Boss 死亡后，Boss 房中间出现下一关传送门。

11\. 靠近传送门显示“按 E 进入第二关”。

12\. 按 E 加载 RunScene\_Level2。

13\. 第二关中玩家正常出生，可移动、闪避、切枪、射击，并受到边界限制。



\---



\## 核心功能



\### 角色选择



\- 中文选角 UI

\- 三名角色数据驱动

\- 角色名、属性、技能、初始武器展示

\- 确认后控制选中角色进入传送门

\- 通过 RunSessionContext 将选中角色传递到运行关卡



\### 三角色主动技能



游侠：战术翻滚  

按键：F  

效果：消耗能量，触发强化战术位移。



守卫：壁垒姿态  

按键：F  

效果：消耗能量，恢复护甲并短时间减伤。



特勤：超频  

按键：F  

效果：消耗能量，短时间提升射速。



Space 保持为所有角色通用闪避。  

F 是角色主动技能。



\### 战斗系统



\- HP / Armor / Energy

\- 双武器槽

\- Q 切换武器

\- 鼠标瞄准射击

\- Projectile 命中确认

\- 小怪受伤、死亡、血条

\- Boss 受伤、死亡

\- Gate / Boss 房流程

\- Boss 死亡后下一关传送门



\### 起始房武器台



第一关 StartArea 中有两个可交互武器台：



左侧武器台：Rapid SMG  

特点：快射低伤。



右侧武器台：Heavy Rail  

特点：慢射高伤。



玩家靠近武器台后按 E，替换当前激活武器槽。  

最终武器状态仍由 PlayerController 管理，武器台只是交互入口。



\### 第二关壳子



RunScene\_Level2 当前是最小可运行场景：



\- 玩家可出生

\- Camera 正常

\- HUD 正常

\- 地面和边界正常

\- WASD / Space / Q / 鼠标射击正常



当前第二关主要用于展示多 RunScene 扩展能力，还未加入完整小怪波次和 Boss。



\---



\## 表现反馈



当前已接入：



\- 枪口火光

\- 命中特效

\- 小怪血条

\- 伤害数字

\- 小怪死亡 FX

\- Boss 死亡 FX

\- Armor break 音效

\- Boss 后传送门提示

\- 中文 Result / 传送提示



这些内容均为 display-only 表现层，不反向控制 gameplay。



\---



\## 技术架构



项目中重点维护 runtime ownership，避免状态源混乱。



\### Runtime Truth Owner



PlayerHealth  

职责：HP / Armor / Energy 的唯一 runtime truth owner。



PlayerController  

职责：武器槽与当前武器的唯一 runtime truth owner。



WeaponController  

职责：firing execution mirror，只执行开火。



Projectile  

职责：projectile 移动、命中确认、伤害派发。



SliceEnemyController  

职责：小怪 HP / death truth owner。



BossHealth  

职责：Boss HP / death event truth owner。



VerticalSliceFlowController  

职责：小怪波次、Gate、Boss、Portal flow authority。



BattleHudController  

职责：HUD display-only orchestrator。



BossRushRuntimeSceneBuilder  

职责：runtime environment / HUD skeleton source。



RunSceneSessionBootstrap  

职责：selected-role 存在时的 final startup writer。



RuntimeSceneHooks  

职责：fallback-only binder / apply layer。



PlayerRoleSkillController  

职责：F 技能输入、冷却、持续时间和调用安全 API。



WeaponPickupStation  

职责：武器台交互入口，不拥有最终武器状态。



NextLevelPortalController  

职责：传送门 trigger + E + LoadScene，不拥有 flow authority。



\---



\## 关键设计原则



\### 1. 状态源唯一



HP / Armor / Energy 只由 PlayerHealth 管。  

当前武器槽只由 PlayerController 管。  

WeaponController 只接收参数并执行开火，不保存武器 truth。



\### 2. 表现层不控制逻辑



以下内容均为 display-only：



\- 小怪血条

\- 伤害数字

\- 命中特效

\- 枪口火光

\- 死亡 FX

\- Result 文案

\- Portal 提示文字



\### 3. Builder-owned 对象统一由 Builder 维护



部分运行时对象由 BossRushRuntimeSceneBuilder 生成或修复，例如：



\- StartArea

\- RoleDisplayA / RoleDisplayB

\- WeaponPickupStation

\- NextLevelPortal

\- PortalToRun

\- RunScene\_Level2 最小场景骨架



因此这类对象不建议只通过 scene 手改作为最终修复。



\### 4. 小步扩展



第二关不是直接硬塞进第一关流程，而是分两步完成：



1\. 先让 RunScene\_Level2 能独立 Play。

2\. 再把第一关 Boss 死亡后的 flow 接到 NextLevelPortal。



这样能降低流程改动风险。



\---



\## 项目结构



Assets/  

&#x20; GameMain/  

&#x20;   Scripts/  

&#x20;     GameLogic/  

&#x20;       Player/  

&#x20;         PlayerHealth.cs  

&#x20;         PlayerController.cs  

&#x20;         PlayerRoleSkillController.cs  

&#x20;       Weapons/  

&#x20;         WeaponController.cs  

&#x20;       Projectile/  

&#x20;         Projectile.cs  

&#x20;       World/  

&#x20;         VerticalSliceFlowController.cs  

&#x20;         SliceEnemyController.cs  

&#x20;         WeaponPickupStation.cs  

&#x20;         NextLevelPortalController.cs  

&#x20;         RoomPortalTrigger.cs  

&#x20;       Boss/  

&#x20;         BossHealth.cs  

&#x20;       Combat/  

&#x20;         DamageText.cs  

&#x20;         DamageTextSpawner.cs  

&#x20;         DeathFxFeedback.cs  

&#x20;         ImpactFlashEffectSpawner.cs  

&#x20;       UI/  

&#x20;         BattleHudController.cs  

&#x20;         ResultPanelController.cs  

&#x20;       CharacterSelect/  

&#x20;         CharacterSelectSceneBootstrap.cs  

&#x20;         CharacterSelectConfirmController.cs  

&#x20;         CharacterSelectPortalController.cs  

&#x20;         CharacterInfoPanelController.cs  

&#x20;       Run/  

&#x20;         RunSceneEntry.cs  

&#x20;         RunSceneSessionBootstrap.cs  

&#x20;       Tools/  

&#x20;         BossRushRuntimeSceneBuilder.cs  

&#x20;         RuntimeSceneHooks.cs  

&#x20;     Data/  

&#x20;       CharacterData.cs  



&#x20; Resources/  

&#x20;   CharacterSelect/  

&#x20;     RangerCharacterData.asset  

&#x20;     GuardianCharacterData.asset  

&#x20;     OperatorCharacterData.asset  



&#x20; Scenes/  

&#x20;   CharacterSelectScene.scene  

&#x20;   RunScene.scene  

&#x20;   RunScene\_Level2.scene  



\---



\## 如何运行



\### 推荐环境



\- Unity / Tuanjie

\- 当前项目记录版本：2022.3.62t7

\- Tuanjie Editor 版本记录：1.8.5



\### Build Settings



需要包含：



Assets/Scenes/CharacterSelectScene.scene  

Assets/Scenes/RunScene.scene  

Assets/Scenes/RunScene\_Level2.scene  



\### 运行方式



1\. 打开项目根目录。

2\. 打开 Assets/Scenes/CharacterSelectScene.scene。

3\. 点击 Play。

4\. 选择角色并确认。

5\. 使用 WASD 移动到传送门处。

6\. 按 E 进入第一关。



\---



\## Play Mode 验证清单



\### CharacterSelectScene



\- 中文 UI 正常

\- 三个角色可选择

\- 点击确认后按钮显示已确认

\- WASD 控制已确认角色

\- 传送门处按 E 进入第一关



\### RunScene



\- WASD 移动正常

\- Space 闪避正常

\- F 角色技能正常

\- Q 切换武器正常

\- 鼠标左键射击正常

\- 武器台按 E 替换当前武器槽正常

\- 小怪受伤 / 死亡正常

\- 小怪血条和伤害数字正常

\- Boss 激活 / 受伤 / 死亡正常

\- Boss 死亡后出现下一关传送门

\- 靠近传送门显示“按 E 进入第二关”

\- 按 E 进入 RunScene\_Level2



\### RunScene\_Level2



\- Player 正常出生

\- HUD 正常

\- Camera 正常

\- WASD / Space / Q / 鼠标射击正常

\- 四面墙能阻挡玩家

\- Console 无红色错误



\---



\## 性能意识



当前项目做了以下性能和稳定性考虑：



\- Projectile 使用运行时对象池，减少射击时频繁创建对象。

\- HUD / FX / 血条 / 伤害数字保持 display-only，不参与伤害和流程判断。

\- WeaponController 不保存武器状态，避免状态分裂。

\- BossRushRuntimeSceneBuilder 统一生成 runtime scene skeleton，减少 scene 引用丢失。

\- 普通调试日志默认关闭，保留必要 warning / error。

\- 短生命周期 FX 当前可接受，后续敌人数增加后可继续迁移到统一 FX pool。



建议后续使用 Unity / Tuanjie Profiler 检查：



\- CPU Main Thread

\- GC Alloc

\- Memory

\- Rendering

\- Audio

\- Object Count

\- Spike



推荐测试路径：



CharacterSelectScene  

\-> RunScene 小怪战斗  

\-> Boss 战  

\-> Boss 死亡传送门  

\-> RunScene\_Level2  



\---



\## 协作与内容接入说明



项目刻意将 gameplay truth 与美术 / 策划可调内容分开。



\### 策划可调内容



CharacterData.asset：



\- HP

\- Armor

\- Energy

\- Dodge 参数

\- 初始武器参数

\- 主动技能配置



WeaponPickupStation payload：



\- 武器名

\- 射速

\- 伤害

\- 子弹速度

\- 生命周期



其他可调内容：



\- Boss / Enemy 数值

\- Gate / Flow 参数



\### 美术可接入内容



\- 角色 sprite

\- 武器 sprite

\- 命中特效 prefab

\- 死亡特效 prefab

\- 传送门 sprite

\- 传送门提示文字

\- UI 文案与面板视觉

\- 武器台视觉



\### 不建议非程序直接修改



\- PlayerHealth

\- PlayerController

\- WeaponController

\- Projectile

\- VerticalSliceFlowController

\- BossRushRuntimeSceneBuilder

\- RunSceneSessionBootstrap



这些模块包含 runtime truth 或流程权威，直接修改容易造成状态源分裂。



\---



\## 已知限制



当前项目是 playable vertical slice，不是完整商业游戏。



已知限制：



\- 第二关目前是最小可运行壳子，没有敌人波次和 Boss。

\- 没有完整随机地图 / Roguelike 房间生成。

\- 没有完整装备背包系统。

\- 没有保存 / 存档 / 养成系统。

\- 没有完整设置菜单和按键重绑定。

\- 当前美术仍为 demo-oriented 接入，部分资源可继续替换。

\- 当前技能缺少专属 HUD 冷却提示和完整动画特效。

\- 第一阶段关卡跳转不保留第一关当前 HP / Armor / pickup 武器状态。

\- 部分 FX 仍是短生命周期 Instantiate / Destroy，敌人数扩大后可考虑池化。



\---



\## 后续可扩展方向



优先级建议：



1\. Level2 加入基础小怪波次。

2\. Level2 加入独立 Boss 或终点 Result。

3\. F 技能补 display-only 特效。

4\. 技能冷却 UI。

5\. 武器台视觉升级，接入激光 / 能量特效。

6\. DamageText / DeathFX 池化。

7\. Profiler 数据记录。

8\. 打包 Windows Build。

9\. 补 Demo 视频和截图。

10\. 增加 README 中的架构图。



不建议短期内直接做：



\- 大型 Inventory 系统

\- 完整随机地牢框架

\- 复杂天赋树

\- 商店系统

\- 大规模资源框架

\- Addressables 迁移



这些更适合在当前 vertical slice 稳定后单独规划。



\---



\## 面试讲解重点



可以从以下角度介绍项目：



1\. 完整闭环  

&#x20;  项目从选角、第一关、小怪、Boss、传送门到第二关，形成可运行 vertical slice。



2\. 状态源边界  

&#x20;  HP / Armor / Energy、武器槽、发射执行、命中伤害、Flow、HUD 各自职责明确。



3\. 表现层解耦  

&#x20;  血条、伤害数字、FX、提示文字都是 display-only，不反向控制 gameplay。



4\. 小步扩展第二关  

&#x20;  先让 RunScene\_Level2 独立运行，再接 Boss 后传送门，避免一次性改乱流程。



5\. 美术 / 策划协作友好  

&#x20;  数值和资源尽量暴露在 CharacterData.asset 或 Inspector 槽位中，避免改核心代码。



6\. 可维护性  

&#x20;  builder-owned 对象通过 BossRushRuntimeSceneBuilder 统一维护，减少 scene 手动引用丢失。



\---



\## 简历描述参考



\- 使用 Unity / Tuanjie 独立实现 2D 俯视角射击 playable vertical slice，包含选角、战斗、关卡推进、Boss 战、新关卡传送和结算闭环。

\- 设计并维护清晰的 runtime ownership，将 HP / Armor / Energy、武器槽、开火执行、命中伤害、HUD 展示和关卡流程解耦。

\- 实现三角色主动技能、双武器槽切换、武器台替换、Projectile 命中、敌人波次、Boss 激活和关卡传送流程。

\- 接入多层 display-only 战斗反馈，包括枪口火光、命中特效、死亡特效、小怪血条、伤害数字、传送门提示和破甲音效。

\- 支持多 RunScene 扩展：第一关 Boss 死亡后通过 NextLevelPortal 进入 RunScene\_Level2，同时保持战斗 truth owner 不被场景流程污染。

\- 注重可维护性与协作接入，将美术资源、数值配置、表现层和运行时状态源分离，降低后续扩展风险。



\---



\## License



当前项目用于个人学习、作品集展示和求职演示。  

如使用第三方美术 / 音频素材，请根据对应素材授权补充说明。

