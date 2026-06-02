# Земли основателя (Founder's Lands)

Реалистичная экономическая градостроительная стратегия на Unity 6 (HDRP).
Полная концепция игры — в `docs/ECONOMIC_STRATEGY_CONCEPT_RU.md` (GDD). Этот файл
описывает **техническую реализацию** и текущее состояние кода.

> Кратко: Manor Lords (деревня и логистика) × Foundation (органическое строительство)
> × Civilization (долгая стратегическая дуга) × Ancient Cities (выживание общины) —
> в виде 3D-стратегии сверху с процедурно меняющейся картой.

---

## Ключевое архитектурное решение: Simulation ↔ Presentation

Игра делится на два слоя с **односторонней зависимостью**:

| Слой | Что это | Зависит от Unity? | Где живёт |
| --- | --- | --- | --- |
| **Simulation** («мозг») | Генерация карты, ресурсы, логистика, жители, сезоны, экономика, разбойники, сохранения. Чистый C#, детерминированный от seed. | **Нет** (`noEngineReferences`) | `Assets/_Project/Scripts/Simulation/` |
| **Presentation** («вид») | MonoBehaviour, HDRP-материалы и шейдеры, Cinemachine-камера, UI, ScriptableObject-обёртки. | Да | `Assets/_Project/Scripts/Presentation/` и др. |

Зачем так (см. GDD §4, §26):

- самую сложную и баг-опасную логику (экономика, логистика на сотни агентов) можно
  **компилировать и юнит-тестировать без Unity и без GPU** — в CI или в песочнице;
- рендер/HDRP заменяемы и не мешают отлаживать «правила игры»;
- ScriptableObject в Unity — это **обёртки для авторинга данных** (data-driven, §20):
  они хранят поля и собирают чистые C#-структуры, которые потребляет симуляция. Баланс
  лежит в данных, а не в коде классов.

`FoundersLands.Simulation.asmdef` собран с `"noEngineReferences": true` — это
**технически запрещает** симуляции тянуть `UnityEngine`, то есть граница слоёв
проверяется компилятором, а не на словах.

---

## Что уже реализовано (Этап: фундамент)

- [x] Структура проекта по GDD §20.
- [x] Разделение Simulation/Presentation через asmdef.
- [x] Детерминированный RNG (SplitMix64) и платформо-стабильный хэш (FNV-1a) —
      основа воспроизводимости карты по seed (§7, §21).
- [x] Детерминированная **послойная генерация карты** (§7):
      высота (fBm + контраст + островной спад) → климат (температура/влажность) →
      гидрология → реки → плодородие почвы → биомы → ресурсы.
- [x] 8 биомов, 9 типов ресурсных залежей, модель тайла с почвой и влажностью.
- [x] **25 проходящих юнит-тестов** (детерминизм, границы значений, согласованность
      воды и биомов, правила размещения ресурсов).
- [x] Консольный previewer карты (ASCII + гистограммы) для headless-проверки.

Что **намеренно отложено** до работы в Unity Editor (нужен GPU/редактор):
рендеринг HDRP, шейдеры (§18), камера (§14), UI (§22), импорт ассетов (§19).

### Пример вывода previewer

```
seed=17658872636640814876  size=100x50  seaLevel=0.32  contentHash=0x97E69BB11C7DCF58
Biomes:  Water 48.6% · Meadow 18.2% · Marsh 12.8% · ConiferForest 6.7% · Highland 6.4% ...
Resources: Fish 154 · Wood 152 · Herbs 76 · Clay 68 · Berries 67 · Stone 41 · IronOre 14 ...
```

---

## Сборка и тесты (headless, без Unity)

Нужен только .NET SDK 8.0+.

```bash
cd SimHarness
dotnet test tests/FoundersLands.Simulation.Tests.csproj -c Release          # юнит-тесты
dotnet run  --project console -c Release -- --seed green-valley --width 100 --height 50  # previewer
```

Подробнее — `SimHarness/README.md`.

---

## Открытие в Unity

1. Поставить **Unity 6.3 LTS** (или актуальную Unity 6 LTS-ветку, GDD §4).
2. Открыть папку репозитория как проект. Unity сам создаст `ProjectSettings/`,
   `Library/` и сгенерирует `.meta`-файлы при первом импорте — после этого их нужно
   закоммитить (в Unity-проектах `.meta` хранятся в git).
3. `Packages/manifest.json` задаёт целевой набор пакетов (HDRP, Cinemachine, Input
   System, AI Navigation, Burst/Collections/Mathematics, Addressables, Test Framework).
   **Версии пакетов** подобраны под Unity 6 и могут потребовать уточнения в Package
   Manager под вашу конкретную сборку 6.3 LTS.

---

## Структура репозитория

```text
Assets/_Project/
  Scripts/
    Simulation/      # ← чистый C#, движок-независимый, тестируется headless
      Core/          #   RNG, стабильный хэш
      Mathematics/   #   координаты, шум, математика
      World/         #   биомы, ресурсы, тайл, генератор карты
    Presentation/    # ← Unity-слой (MonoBehaviour/HDRP/UI), добавляется в Editor
    Core/ Economy/ Construction/ Terrain/ AI/ UI/ SaveLoad/ Rendering/ Shaders/
  ScriptableObjects/ # data-driven определения (Buildings, Resources, Recipes, ...)
  Art/ Audio/ Materials/ Shaders/ Prefabs/ Scenes/ UI/ VFX/
Packages/manifest.json
SimHarness/          # headless-проекты для сборки/тестов симуляции (вне Assets/)
docs/                # GDD и проектная документация
```

---

## Дорожная карта (по GDD §24)

| Этап | Содержание | Статус |
| --- | --- | --- |
| Фундамент | Scaffold, Simulation/Presentation, генерация карты, тесты | **в работе** |
| MVP (§24.1) | Ресурсы, жители-агенты, склады, строительство по этапам, сезоны, сохранение | дальше |
| Вертикальный срез (§24.2) | HDRP, погода, поля, цепочки, рынок, первые разбойники | — |
| Alpha / Beta / EA | по GDD §24.3–24.5 | — |

Принцип (§25, §28): сначала рабочая экономическая петля в числах, потом контент и
красота. Если базовая петля скучна — чиним её, а не добавляем здания.
