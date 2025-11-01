# Test for Manerai Inc — README

Проект — небольшой прототип на Unity с системой ударов от первого лица, визуальными и звуковыми эффектами попаданий, а также физическим контроллером «выпрямления» головы. В проекте используются Zenject (DI) и UniRx (реактивные обновления) для простоты связывания компонентов и управления жизненным циклом.

## Требования
- Unity `2022.3.44f1` (LTS)
- Пакеты/плагины:
  - Zenject (в `Assets/Plugins/Zenject`)
  - UniRx (в `Assets/Plugins/UniRx`)
  - TextMeshPro (через Package Manager)

## Как запустить
1. Откройте проект в Unity `2022.3.44f1`.
2. Откройте сцену `Assets/Scenes/SampleScene.unity`.
3. Запустите сцену (Play). ЛКМ — удар. 

При необходимости для отладки есть сцена `Assets/Scenes/Calc.unity`.

## Управление
- ЛКМ (`Mouse0`) — нанести удар. Удары чередуются левой/правой рукой.
- Время перезарядки удара — по умолчанию `0.35 c`.

## Основные механики
- Чередование ударов: каждый новый удар триггерит соответствующую анимацию (`LeftHit` / `RightHit`).
- Окно урона (gate): во время ключевого события в анимации открывается «окно попадания» для коллайдеров кулаков, что исключает ложные срабатывания в остальное время.
- Детекция попаданий вперёд: сферический луч (`SphereCastAll`) от позиции кулака вперёд на заданную дистанцию. По первому попавшему коллайдеру воспроизводится эффект попадания.
- Частицы крови/удара: из пула частиц инстансы переиспользуются и автоматически деактивируются после окончания воспроизведения.
- Звук удара: по каждому попаданию проигрываются SFX (чередуются разные клипы для левой/правой руки).
- Выпрямление головы: `ConfigurableJoint` со Slerp‑приводом поддерживает ориентацию головы вверх с настраиваемым «бустом» при больших отклонениях.

## Структура сцен/контента
- Сцены: `SampleScene.unity`, `Calc.unity`.
- Префабы:
  - `Assets/Prefabs/Hands.prefab` — руки игрока с аниматором и коллайдерами кулаков.
  - `Assets/Prefabs/Ogre.prefab` — цель/манекен для попаданий.
  - `Assets/Prefabs/BloodSpray.prefab` — эффект частиц попадания.
  - (Дополнительно) `Assets/Prefabs/HitProjectile.prefab` — заготовка под снаряд/эффект (по коду не используется).
- Анимации и контроллер:
  - `Assets/Animation/Idle.anim`, `LeftHit.anim`, `RightHit.anim`
  - `Assets/Animation/Hands.controller` — триггеры `LeftHit` / `RightHit`.
- Аудио: `Assets/Audio/heavy-blunt-blow.wav`, `the-blow-is-muffled-decisive.wav`.

## Компоненты и код
Ниже — ключевые скрипты и их назначение.

### ForwardHitter (`Assets/Scripts/ForwardHitter.cs`)
- Ввод: следит за `Mouse0` и соблюдает перезарядку `hitCooldown`.
- Анимации: выставляет триггер `LeftHit` или `RightHit` и чередует руки.
- События анимации: методы `HitEventLeft()` / `HitEventRight()` вызываются из таймлайна клипа (Animation Event) и:
  - открывают «окно попадания» у соответствующего эмиттера коллизий на `gateDuration`;
  - выполняют проверку попадания вперёд (сферическая/линейная трассировка) и создают эффект частиц через пул;
  - проигрывают звук удара.
- Настройки детекции: `hitRange`, `hitRadius` (если `hitRadius` = 0 — используется `Raycast`).
- Слои целей: `enemyMask` — маска слоёв, по которым допустимы попадания.

### HitCollisionEmitter (`Assets/Scripts/HitCollisionEmitter.cs`)
- Требует `Collider` на объекте (например, на косточке кулака).
- Фильтрация: `targetMask` — только столкновения с этими слоями создают эффект.
- Gate‑режим: если `requireGate = true`, эмиттер активен только в «окно», открытое `StartGate(duration)`.
- Защита от спама: `emitCooldown` — минимальный интервал между эмитами для одного эмиттера.
- Геометрия точки эффекта: аккуратно вычисляет позицию/нормаль удара по контактам или через `Raycast` к коллайдеру, чтобы спавнить эффект на поверхности.
- Пул частиц берётся из поля (`poolOverride`) или внедряется через Zenject.

### HitParticlePool (`Assets/Scripts/HitParticlePool.cs`)
- Пулит `particlePrefab` размером `poolSize` под родителем `poolParent` (по умолчанию — текущий объект).
- Спавн: смещает позицию вдоль нормали на `surfaceOffset`, при `alignToNormal = true` разворачивает эффект «от поверхности».
- Воспроизведение: перезапускает все `ParticleSystem` на инстансе и с помощью UniRx‑таймера деактивирует объект по окончании.
- Время жизни: вычисляет по `duration + startLifetime.max` всех систем; при неудаче — `fallbackLifetime`.

### HeadUprightController (`Assets/Scripts/HeadUprightController.cs`)
- Работает с `ConfigurableJoint` и телом `Rigidbody` головы.
- Цель: удерживать ось «вверх» головы к мировому `worldUp` с мёртвой зоной `deadZoneDeg`.
- Привод: настраиваемые `slerpSpring`, `slerpDamper`, `slerpMaxForce`.
- Буст: при угле больше `boostAngleDeg` множит пружину/демпфер (`boostSpringMul`, `boostDamperMul`) для ускоренного возврата.
- Ограничение физики: повышает `maxAngularVelocity` у `Rigidbody` головы.

### TestSceneInstaller (`Assets/Scripts/Installers/TestSceneInstaller.cs`)
- Zenject‑инсталлер сцены: биндинг `HitParticlePool` как `AsSingle()` из иерархии, если не привязан.

## Настройка анимаций (важно)
Чтобы удары работали корректно, убедитесь, что в клипах `LeftHit` и `RightHit` выставлены Animation Events, вызывающие методы компонента `ForwardHitter`:
- Для левой руки: `HitEventLeft`
- Для правой руки: `HitEventRight`
Эти события должны попадать на фазу удара (контакта), чтобы открывалось «окно» коллизий и спавнились эффекты.

## Слои и маски
- Объекты‑цели должны быть на слоях, входящих в `enemyMask` у `ForwardHitter` и в `targetMask` у `HitCollisionEmitter`.
- Кулаки/коллайдеры рук должны иметь `Collider` (и, при необходимости, `Rigidbody`) для корректной генерации событий столкновений.

## Тюнинг параметров
- `ForwardHitter`: `hitCooldown`, `hitRange`, `hitRadius`, `gateDuration`, ссылки на `leftFist`/`rightFist`, `enemyMask`.
- `HitCollisionEmitter`: `targetMask`, `requireGate`, `emitCooldown`.
- `HitParticlePool`: `particlePrefab`, `poolSize`, `surfaceOffset`, `alignToNormal`, `extraLifetime`, `fallbackLifetime`.
- `HeadUprightController`: `deadZoneDeg`, `slerpSpring`, `slerpDamper`, `slerpMaxForce`, `boost*`, `maxAngularVelocity`.

## Известные заметки
- В `Assets/Plugins` присутствуют исходники Zenject и UniRx, а также примеры Zenject — они не участвуют в логике демо‑сцены.
- Префаб `HitProjectile.prefab` не используется текущим кодом, оставлен как заготовка/ресурс.

## Структура проекта (основное)
- Код: `Assets/Scripts/**`
- Сцены: `Assets/Scenes/**`
- Префабы и эффекты: `Assets/Prefabs/**`
- Анимации: `Assets/Animation/**`
- Аудио: `Assets/Audio/**`
- Плагины: `Assets/Plugins/**` (Zenject, UniRx)

## Лицензирование
Материалы, входящие в Unity (TextMeshPro/EmojiOne и т. п.), используются по их базовым лицензиям. SFX/ассеты в папке `Assets/Audio`/`Assets/Prefabs` — для прототипа. При публичном распространении проверьте/уточните права на сторонние ресурсы.

---
Если нужен раздел «Как расширить» (например, урон/здоровье цели, камеру от первого лица, UI‑индикаторы), скажите — добавлю и настрою.

