# Визуал огненной стрелы для Valheim

Основной префаб: `Assets/Magic_plus/Prefabs/fire_arrow_visual.prefab`.
Два эффекта FullOpaqueFire используют отдельный материал
`Assets/Magic_plus/materials/FireParticles.mat` и шейдер
`Assets/Magic_plus/Shaders/FullOpaqueFire.shader` (MagicPlus/FullOpaqueFire).
Исходные FullOpaqueFire/M_Particles и S_Particle.shadergraph не редактировались.

В старом Windows-пакете Built-in секция Shader Graph не содержала проходов,
что приводило к фиолетовому огню. Новый самостоятельный unlit shader сохраняет
палитру Blackbody/Hue/Saturation, читает Custom1.Y (температура) из UV0.w и
не требует текстур или работающих URP-проходов в игре. Он также отображается
в URP-проекте через общий unlit pass.

- Цвет/затухание: Start Color и Color over Lifetime у ParticleSystem.
- Яркость: FireParticles → Fire intensity (по умолчанию 1).
- Температура: Custom Data → Custom1.Y (6000).
- Оттенок: FireParticles → Hue offset in degrees (-10).
- GPU Instancing у обоих ParticleSystemRenderer выключен. Материал ожидает
  streams Position/Normal/Color/UV/Custom1XY и CPU данные частиц.
- Меши, режимы Mesh/Stretch, Noise, эмиссия и размер модели сохранены.

Редактор для игры: Unity 6000.0.75f1. Windows Build Support установлен.
Выделить основной prefab в Project → MagicPlus → Build selected arrow for Windows.
Результат: `Builds/MagicPlus/Windows/magicplus_visuals`.

Нативный CLI: `/Users/dima/.unity/bin/unity`; управление этим проектом требует
явного `--project-path '/Users/dima/valheim mods/UnityMagicPlus/Magic_plus'`.
Подробности проверки и процесса — `tools/unity/README.md` в общей рабочей папке.
Новый вид проверен в превью URP/Built-in; финальный вид/сеть проверяются в Valheim.
