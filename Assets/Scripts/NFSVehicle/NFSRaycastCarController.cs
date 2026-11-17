using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RedLine.NFS
{
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody))]
    public sealed class NFSRaycastCarController : MonoBehaviour
    {
        [Serializable]
        public sealed class Wheel
        {
            [InspectorName("Опора подвески")]
            [Tooltip("Задаёт начало луча подвески. Направление луча — вниз по локальной оси Y кузова.")]
            public Transform SuspensionPoint;
            [InspectorName("Ось вращения колеса")]
            [Tooltip("Объект, который поворачивается и вращается вместе с колесом. Поправку ориентации модели задавайте на его дочернем объекте.")]
            public Transform Visual;
            [InspectorName("Коллайдер колеса")]
            [Tooltip("Компонент WheelCollider для этого колеса. Обязателен для всех четырёх колёс в режиме WheelCollider.")]
            public WheelCollider PhysicsCollider;
            [InspectorName("Смещение опоры")]
            [Tooltip("Координаты крепления относительно кузова, в м. Используются, если точка крепления подвески не назначена.")]
            public Vector3 LocalAnchor;
            [InspectorName("Поворотное колесо")]
            [Tooltip("Включает поворот колеса рулём. Ручник действует только на неуправляемые колёса.")]
            public bool Steers;
            [InspectorName("Ведущее колесо")]
            [Tooltip("Включает передачу тяги на колесо. В стандартном пресете ведущие колёса задние.")]
            public bool Driven;
            [Min(.05f), InspectorName("Радиус колеса")]
            [Tooltip("Радиус качения, в м. Больший радиус удлиняет проверку опоры. У задних колёс он также снижает расчётные обороты двигателя при той же скорости.")]
            public float Radius = .34f;
            [NonSerialized] internal Quaternion visualBindRotation;
            [NonSerialized] internal float spin;
            [NonSerialized] internal WheelContact contact;
            [NonSerialized] internal WheelFrictionCurve sidewaysFriction;
        }

        public struct WheelContact
        {
            public bool Grounded;
            public Vector3 Point, Normal;
            public float SpringLength, Compression, NormalLoad;
            public Collider Collider;
        }

        public struct DriverInput
        {
            public float Throttle, BrakeReverse, Steer;
            public bool Handbrake, Nitro;
            public DriverInput(float throttle, float brakeReverse, float steer, bool handbrake = false, bool nitro = false)
            { Throttle = throttle; BrakeReverse = brakeReverse; Steer = steer; Handbrake = handbrake; Nitro = nitro; }
        }

        [Header("Управление с клавиатуры и геймпада")]
        [InspectorName("Газ")]
        [Tooltip("W, стрелка вверх или правый триггер геймпада.")] public InputAction Throttle = new InputAction("Throttle", InputActionType.Value, expectedControlType: "Axis");
        [InspectorName("Тормоз / задний ход")]
        [Tooltip("S, стрелка вниз или левый триггер геймпада. Удерживайте после остановки для включения заднего хода.")] public InputAction BrakeReverse = new InputAction("BrakeReverse", InputActionType.Value, expectedControlType: "Axis");
        [InspectorName("Руль")]
        [Tooltip("A/D, стрелки влево и вправо или ось X левого стика.")] public InputAction Steer = new InputAction("Steer", InputActionType.Value, expectedControlType: "Axis");
        [InspectorName("Ручник")]
        [Tooltip("Пробел или нижняя кнопка геймпада.")] public InputAction Handbrake = new InputAction("Handbrake", InputActionType.Button);
        [InspectorName("Нитро")]
        [Tooltip("Левый Shift или правая кнопка геймпада.")] public InputAction Nitro = new InputAction("Nitro", InputActionType.Button);

        [Header("Кузов и сопротивление движению")]
        [Min(100), InspectorName("Масса кузова")]
        [Tooltip("Масса автомобиля, в кг. Чем она больше, тем выше инерция и нагрузка на подвеску.")]
        public float Mass = 1400;
        [InspectorName("Центр масс")]
        [Tooltip("Координаты относительно кузова, в м. Большее Y поднимает центр масс и усиливает крен. Используется, если маркер не назначен.")]
        public Vector3 CenterOfMass = new Vector3(0, -.25f, .05f);
        [Range(0, 31), InspectorName("Слой автомобиля")]
        [Tooltip("Номер слоя собственных коллайдеров. Лучевая подвеска исключает этот слой из поиска опоры.")]
        public int VehicleLayer = 2;
        [InspectorName("Слои опорной поверхности")]
        [Tooltip("Слои, на которых лучевая подвеска ищет опору. Слой автомобиля исключается.")]
        public LayerMask GroundLayers = Physics.DefaultRaycastLayers;
        [Min(0), InspectorName("Аэродинамическое сопротивление")]
        [Tooltip("Коэффициент сопротивления воздуха, в Н/(м/с)². При увеличении машина сильнее теряет скорость, особенно на высоких скоростях.")]
        public float AeroDrag = .42f;
        [Min(0), InspectorName("Сопротивление качению")]
        [Tooltip("Замедление на опорной поверхности, в м/с². При увеличении машина быстрее теряет скорость накатом.")]
        public float RollingResistance = .12f;
        [Range(1, 4), InspectorName("Множитель гравитации")]
        [Tooltip("Чем выше множитель, тем сильнее гравитация автомобиля при включённой гравитации Rigidbody. Лучевая подвеска усиливает пружины и демпферы, сохраняя статическую просадку.")]
        public float GravityMultiplier = 3;

        [Header("Колёса и подвеска")]
        [InspectorName("Колёса")]
        [Tooltip("Порядок четырёх колёс — переднее левое, переднее правое, заднее левое, заднее правое.")]
        public Wheel[] Wheels = DefaultWheels();
        [InspectorName("Использовать WheelCollider")]
        [Tooltip("Включает физику WheelCollider вместо лучевой подвески и её сил сцепления. Требуются четыре назначенных коллайдера.")]
        public bool UseWheelColliders;
        [InspectorName("Маркер центра масс")]
        [Tooltip("Если маркер назначен, его позиция задаёт центр масс при запуске.")]
        public Transform CenterOfMassTransform;
        [Min(.05f), InspectorName("Длина подвески без нагрузки")]
        [Tooltip("Длина пружины без нагрузки, в м. При увеличении растёт клиренс в лучевом режиме. В режиме WheelCollider служит базой для расчёта сжатия.")]
        public float SuspensionRestLength = .36f;
        [Min(.02f), InspectorName("Ход подвески")]
        [Tooltip("Ход от положения без нагрузки в каждую сторону, в м. При увеличении растёт запас сжатия и вывешивания лучевой подвески.")]
        public float SuspensionTravel = .22f;
        [Min(1000), InspectorName("Жёсткость пружин")]
        [Tooltip("Жёсткость на колесо, в Н/м. При увеличении лучевая подвеска меньше проседает, а стабилизатор сильнее сопротивляется крену в обоих режимах.")]
        public float SpringStiffness = 38000;
        [Min(0), InspectorName("Демпфирование подвески")]
        [Tooltip("Коэффициент демпфирования на колесо, в Н/(м/с). При увеличении колебания лучевой подвески гасятся сильнее.")]
        public float DamperStiffness = 4200;
        [Range(0, 1), InspectorName("Радиус лучевой проверки")]
        [Tooltip("Радиус сферы для поиска опоры, в м. При увеличении зона поиска шире, но не больше 80% радиуса колеса. Ноль включает обычный луч.")]
        public float CastRadius = .1f;
        [Range(0, 1), InspectorName("Жёсткость стабилизатора крена")]
        [Tooltip("При увеличении разница хода левого и правого колёс создаёт большее усилие против крена. Оба колеса оси должны иметь контакт.")]
        public float AntiRoll = .45f;

        [Header("Двигатель и автоматическая КПП")]
        [Min(10), InspectorName("Предел скорости вперёд")]
        [Tooltip("Предел без нитро, в км/ч. При увеличении тяга начинает спадать на более высокой скорости.")]
        public float MaxSpeedKmh = 320;
        [Min(1), InspectorName("Пиковый момент двигателя")]
        [Tooltip("Базовый крутящий момент до применения кривой, в Н·м. При увеличении растёт расчётная тяга на ведущих колёсах.")]
        public float EngineTorque = 540;
        [InspectorName("Кривая крутящего момента")]
        [Tooltip("По X — доля максимальных оборотов. По Y — множитель момента. Поднимите кривую там, где нужна большая тяга.")]
        public AnimationCurve TorqueCurve = new AnimationCurve(new Keyframe(0, .65f), new Keyframe(.45f, 1), new Keyframe(.8f, .95f), new Keyframe(1, .55f));
        [InspectorName("Передаточные числа КПП")]
        [Tooltip("Элемент 0 — отрицательное число заднего хода, остальные — передачи вперёд. Большее число по модулю даёт больше тяги и оборотов при той же скорости.")]
        public float[] GearRatios = { -3.4f, 3.65f, 2.15f, 1.55f, 1.18f, .96f, .78f };
        [Min(.1f), InspectorName("Передаточное число главной пары")]
        [Tooltip("При увеличении растут тяга и обороты двигателя при той же скорости на любой передаче.")]
        public float FinalDrive = 3.8f;
        [Min(100), InspectorName("Обороты переключения вверх")]
        [Tooltip("Порог, в об/мин. При увеличении автомат дольше держит низкую передачу при разгоне.")]
        public float ShiftUpRPM = 7200;
        [Min(100), InspectorName("Обороты переключения вниз")]
        [Tooltip("Порог, в об/мин. При увеличении автомат раньше понижает передачу, если обороты после переключения достаточно низкие.")]
        public float ShiftDownRPM = 3200;
        [Min(100), InspectorName("Обороты холостого хода")]
        [Tooltip("Нижний предел, в об/мин. При увеличении двигатель держит более высокие обороты при отпущенном газе.")]
        public float IdleRPM = 950;
        [Min(1000), InspectorName("Максимальные обороты")]
        [Tooltip("Верхний предел, в об/мин. При увеличении расширяется диапазон оборотов, по которому читается кривая момента.")]
        public float MaxRPM = 8000;
        [Range(.05f, 1), InspectorName("Время переключения передачи")]
        [Tooltip("Длительность переключения, в с. При увеличении дольше сохраняется ослабленная тяга.")]
        public float ShiftDelay = .18f;
        [Range(.1f, 1), InspectorName("Задержка смены направления")]
        [Tooltip("Время удержания педали противоположного направления при отпущенной второй педали и скорости ниже 2,2 км/ч, в с. Чем больше, тем дольше ожидание.")]
        public float DirectionChangeDelay = .22f;
        [Min(1), InspectorName("Предел скорости заднего хода")]
        [Tooltip("Предел, в км/ч. При увеличении машина может быстрее ехать задним ходом.")]
        public float ReverseMaxSpeedKmh = 40;
        [Header("Торможение")]
        [Min(1), InspectorName("Базовое тормозное замедление")]
        [Tooltip("Базовое замедление, в м/с². При увеличении растёт усилие рабочих тормозов и ручника.")]
        public float BrakeAcceleration = 15;
        [Range(1, 4), InspectorName("Множитель силы рабочих тормозов")]
        [Tooltip("Чем выше множитель, тем больше общее усилие рабочих тормозов при том же нажатии педали. Коэффициент одинаков для всех колёс.")]
        public float BrakeStrengthMultiplier = 2;

        [Header("Руление и сцепление шин")]
        [Range(5, 50), InspectorName("Максимальный угол поворота колёс")]
        [Tooltip("Базовый угол при полном руле, в градусах. При увеличении колёса поворачиваются сильнее, особенно на малой скорости.")]
        public float MaxSteerAngle = 32;
        [InspectorName("Кривая угла поворота от скорости")]
        [Tooltip("По X — доля предела скорости без нитро. По Y — множитель угла. Поднимите кривую там, где нужен больший угол поворота колёс.")]
        public AnimationCurve SteeringBySpeed = new AnimationCurve(new Keyframe(0, 1), new Keyframe(.3f, .48f), new Keyframe(.7f, .2f), new Keyframe(1, .13f));
        [Range(.5f, 1), InspectorName("Множитель угла поворота колёс на высокой скорости")]
        [Tooltip("Чем ближе к 1, тем больший угол доступен на высокой скорости. Множитель плавно вступает в силу после 65% предела скорости без нитро.")]
        public float HighSpeedSteeringScale = .8f;
        [Range(1, 15), InspectorName("Боковое сцепление спереди")]
        [Tooltip("Коэффициент гашения бокового скольжения управляемых колёс в лучевом режиме, в 1/с. При увеличении колёса сильнее сопротивляются сносу.")]
        public float FrontGrip = 9;
        [Range(1, 15), InspectorName("Боковое сцепление сзади")]
        [Tooltip("Коэффициент гашения бокового скольжения неуправляемых колёс в лучевом режиме, в 1/с. При увеличении сцепление вне заноса растёт, а в режиме WheelCollider усиливается его снижение при ручнике или сбросе газа.")]
        public float RearGrip = 7.5f;
        [Range(.1f, 6), InspectorName("Боковое сцепление в скольжении")]
        [Tooltip("Чем выше значение, тем меньше скользят неуправляемые колёса при ручнике или резком сбросе газа в повороте. В лучевом режиме измеряется в 1/с. Для WheelCollider используется отношение к заднему сцеплению.")]
        public float DriftRearGrip = 1.65f;
        [Range(.5f, 3), InspectorName("Коэффициент трения шин")]
        [Tooltip("При увеличении растёт предельная сила сцепления относительно нагрузки на колесо в лучевом режиме. У WheelCollider этот параметр задаёт исходное трение при сборке префаба.")]
        public float TyreFriction = 1.65f;
        [Range(1, 15), InspectorName("Отклик руля")]
        [Tooltip("Скорость изменения команды руля, в 1/с. При увеличении колёса быстрее реагируют на поворот и отпускание руля.")]
        public float SteeringResponse = 7;

        [Header("Занос")]
        [Range(3, 35), InspectorName("Порог входа в занос")]
        [Tooltip("При увеличении требуется больший угол скольжения, в градусах. Контроллер определяет занос на передаче переднего хода, выше 30 км/ч и при контакте хотя бы двух колёс.")]
        public float DriftThreshold = 10;
        [Range(15, 70), InspectorName("Целевой угол в заносе")]
        [Tooltip("Угол скольжения при полном руле, в градусах. При увеличении стабилизация удерживает больший угол на дуге. При возврате руля к центру целевой угол стремится к нулю.")]
        public float TargetDriftAngle = 34;
        [Range(35, 85), InspectorName("Порог коррекции глубокого заноса")]
        [Tooltip("Угол скольжения, в градусах, после которого коррекция стремится к предельной скорости разворота. При увеличении коррекция глубокого заноса включается позже.")]
        public float MaxDriftAngle = 58;
        [Range(0, 15), InspectorName("Сила стабилизации в заносе")]
        [Tooltip("Коэффициент коррекции скорости разворота, в 1/с. При увеличении машина активнее держит целевой угол на дуге и быстрее выпрямляется при отпускании руля, пока определяется занос.")]
        public float DriftAssistStrength = 5;
        [Range(.3f, 3), InspectorName("Предел скорости разворота")]
        [Tooltip("Максимальная скорость вращения вокруг нормали к опоре, в рад/с. При увеличении машина может быстрее разворачиваться на поверхности.")]
        public float MaxYawRate = 1.55f;
        [Range(0, 4), InspectorName("Добавочное ускорение в заносе")]
        [Tooltip("Ускорение вперёд при полном газе, в м/с². При увеличении машина получает больше тяги в заносе при газе выше 10% и скорости ниже предела без нитро.")]
        public float DriftForwardBoost = 1.2f;

        [Header("Нитро")]
        [Min(1), InspectorName("Ёмкость нитро")]
        [Tooltip("Максимальный заряд, в единицах. При увеличении полного заряда хватает на более длинное ускорение.")]
        public float NitroCapacity = 100;
        [Min(.1f), InspectorName("Расход нитро")]
        [Tooltip("Расход заряда, в ед./с. При увеличении заряд заканчивается быстрее.")]
        public float NitroBurnRate = 25;
        [Min(0), InspectorName("Восстановление нитро")]
        [Tooltip("Пополнение заряда, в ед./с. При увеличении заряд возвращается быстрее при отпущенной кнопке нитро, скорости выше 50 км/ч и контакте хотя бы двух колёс.")]
        public float NitroRegenRate = 6;
        [Min(1), InspectorName("Множитель пополнения в заносе")]
        [Tooltip("При увеличении нитро быстрее восстанавливается в заносе при выполнении условий пополнения.")]
        public float NitroDriftRegenMultiplier = 2.5f;
        [Range(1, 3), InspectorName("Множитель тяги при нитро")]
        [Tooltip("Чем выше множитель, тем больше добавочная тяга вперёд во время работы нитро. При 1 добавочной тяги нет.")]
        public float NitroForceMultiplier = 1.7f;
        [Min(0), InspectorName("Прибавка к скорости с нитро")]
        [Tooltip("Прибавка к пределу скорости, в км/ч. При увеличении с активным нитро тяга сохраняется до более высокой скорости.")]
        public float NitroMaxSpeedBonus = 50;

        [Header("Прижим и стабилизация")]
        [Min(0), InspectorName("Прижимная сила")]
        [Tooltip("Прижим на единицу скорости, в Н/(м/с). При увеличении машина сильнее прижимается к поверхности при контакте хотя бы одного колеса.")]
        public float DownforceFactor = 75;
        [Range(0, 40), InspectorName("Выравнивание кузова в воздухе")]
        [Tooltip("Коэффициент углового ускорения на радиан наклона, в 1/с². При увеличении кузов активнее возвращается в вертикальное положение в воздухе.")]
        public float AirLevelStrength = 16;
        [Range(0, 15), InspectorName("Гашение вращения в воздухе")]
        [Tooltip("Коэффициент демпфирования, в 1/с. При увеличении быстрее гасится вращение кузова по крену и тангажу в воздухе.")]
        public float AirAngularDamping = 5;
        [Range(0, 1), InspectorName("Сохранение скорости вдоль стены")]
        [Tooltip("Доля потерянной скорости вдоль стены, которую возвращает контроллер. При увеличении скользящее столкновение сильнее сохраняет ход.")]
        public float WallSlideAssist = 1;

        public Rigidbody Body { get; private set; }
        public float EffectiveSpringStiffness => SpringStiffness * GravityMultiplier;
        public float SpeedKmh { get; private set; }
        public float ForwardSpeed { get; private set; }
        public float CurrentRPM { get; private set; }
        public int CurrentGear { get; private set; } = 1;
        public float SlipAngle { get; private set; }
        public bool IsDrifting { get; private set; }
        public bool IsShifting => shiftTimer > 0;
        public bool IsNitroActive { get; private set; }
        public float NitroAmount { get; private set; }
        public float NitroNormalized => NitroAmount / Mathf.Max(1, NitroCapacity);
        public int GroundedWheelCount { get; private set; }
        public float ThrottleInput { get; private set; }
        public float BrakeInput { get; private set; }
        public float SteerInput { get; private set; }
        public bool HandbrakeInput { get; private set; }
        public bool IsBurningOut => GroundedWheelCount >= 2 && CurrentGear > 0 && ThrottleInput > .7f && SpeedKmh < 35;
        public bool IsWallSliding => Time.time - wallTime < .12f && SpeedKmh > 30;
        public Vector3 WallPoint { get; private set; }
        public Vector3 WallNormal { get; private set; }

        private readonly RaycastHit[] castHits = new RaycastHit[24];
        private DriverInput externalInput;
        private bool useExternalInput;
        private float shiftTimer, shiftCooldown, directionTimer, rearDriftBlend, liftOffTimer, previousThrottle;
        private float wheelSteerAngle, wallTime = -100;
        private Vector3 groundNormal = Vector3.up, preStepVelocity, wallIncoming;
        private bool pendingWall;
        private PhysicsMaterial bodyMaterial;

        public static Wheel[] DefaultWheels() => new[] {
            new Wheel { LocalAnchor = new Vector3(-.86f, .1f, 1.35f), Steers = true },
            new Wheel { LocalAnchor = new Vector3(.86f, .1f, 1.35f), Steers = true },
            new Wheel { LocalAnchor = new Vector3(-.86f, .1f, -1.35f), Driven = true },
            new Wheel { LocalAnchor = new Vector3(.86f, .1f, -1.35f), Driven = true }
        };

        private void Reset()
        {
            Wheels = DefaultWheels();
            var box = GetComponent<BoxCollider>();
            if (box) { box.size = new Vector3(1.8f, .55f, 4.2f); box.center = Vector3.zero; }
            EnsureBindings();
        }

        private void OnValidate()
        {
            Mass = Mathf.Max(100, Mass); MaxSpeedKmh = Mathf.Max(10, MaxSpeedKmh);
            SuspensionRestLength = Mathf.Max(.05f, SuspensionRestLength); SuspensionTravel = Mathf.Max(.02f, SuspensionTravel);
            SpringStiffness = Mathf.Max(1000, SpringStiffness); DamperStiffness = Mathf.Max(0, DamperStiffness);
            GravityMultiplier = Mathf.Clamp(GravityMultiplier, 1, 4); BrakeStrengthMultiplier = Mathf.Clamp(BrakeStrengthMultiplier, 1, 4);
            HighSpeedSteeringScale = Mathf.Clamp(HighSpeedSteeringScale, .5f, 1);
            IdleRPM = Mathf.Max(100, IdleRPM); MaxRPM = Mathf.Max(IdleRPM + 1000, MaxRPM);
            ShiftUpRPM = Mathf.Clamp(ShiftUpRPM, IdleRPM + 500, MaxRPM - 100);
            ShiftDownRPM = Mathf.Clamp(ShiftDownRPM, IdleRPM + 100, ShiftUpRPM - 500);
            NitroCapacity = Mathf.Max(1, NitroCapacity); NitroBurnRate = Mathf.Max(.1f, NitroBurnRate);
            if (GearRatios == null || GearRatios.Length < 2) GearRatios = new[] { -3.4f, 3.65f, 2.15f, 1.55f, 1.18f, .96f, .78f };
            GearRatios[0] = -Mathf.Max(.1f, Mathf.Abs(GearRatios[0]));
            for (int i = 1; i < GearRatios.Length; i++) GearRatios[i] = Mathf.Max(.1f, Mathf.Abs(GearRatios[i]));
            if (Wheels == null || Wheels.Length != 4) Wheels = DefaultWheels();
            var defaults = DefaultWheels();
            for (int i = 0; i < 4; i++) { if (Wheels[i] == null) Wheels[i] = defaults[i]; Wheels[i].Radius = Mathf.Max(.05f, Wheels[i].Radius); }
            EnsureBindings();
        }

        private void Awake()
        {
            OnValidate(); Body = GetComponent<Rigidbody>();
            Body.mass = Mass;
            Body.centerOfMass = CenterOfMassTransform ? transform.InverseTransformPoint(CenterOfMassTransform.position) : CenterOfMass;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.linearDamping = 0; Body.angularDamping = .12f; Body.maxAngularVelocity = 5;
            Body.maxDepenetrationVelocity = 5;
            Body.solverIterations = 12; Body.solverVelocityIterations = 4;
            foreach (var collider in GetComponentsInChildren<Collider>())
            {
                collider.gameObject.layer = VehicleLayer;
                if (collider is WheelCollider) continue;
                // Keep the persistent material assigned by the prefab tool, including its bounce.
                if (!collider.sharedMaterial)
                {
                    if (!bodyMaterial) bodyMaterial = new PhysicsMaterial("NFS chassis — zero friction") { dynamicFriction = 0, staticFriction = 0,
                        bounciness = 0, frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum };
                    collider.sharedMaterial = bodyMaterial;
                }
                collider.contactOffset = .025f;
            }
            bool complete = true;
            foreach (var wheel in Wheels) complete &= wheel.PhysicsCollider != null;
            if (UseWheelColliders && !complete)
            {
                Debug.LogError("WheelCollider mode requires four assigned wheels. Vehicle disabled to avoid duplicate suspension forces.", this);
                enabled = false; return;
            }
            foreach (var wheel in Wheels)
            {
                if (wheel.Visual) wheel.visualBindRotation = wheel.Visual.localRotation;
                if (!wheel.PhysicsCollider) continue;
                wheel.PhysicsCollider.enabled = UseWheelColliders;
                wheel.sidewaysFriction = wheel.PhysicsCollider.sidewaysFriction;
                wheel.PhysicsCollider.ConfigureVehicleSubsteps(10, 5, 8);
            }
            NitroAmount = NitroCapacity; CurrentRPM = IdleRPM;
        }

        private void OnEnable() { EnsureBindings(); Throttle.Enable(); BrakeReverse.Enable(); Steer.Enable(); Handbrake.Enable(); Nitro.Enable(); }
        private void OnDisable()
        {
            Throttle?.Disable(); BrakeReverse?.Disable(); Steer?.Disable(); Handbrake?.Disable(); Nitro?.Disable(); IsNitroActive = false;
            if (UseWheelColliders && Wheels != null) foreach (var wheel in Wheels)
                if (wheel?.PhysicsCollider) { wheel.PhysicsCollider.motorTorque = 0; wheel.PhysicsCollider.brakeTorque = 0; }
        }
        private void OnDestroy()
        {
            Throttle?.Dispose(); BrakeReverse?.Dispose(); Steer?.Dispose(); Handbrake?.Dispose(); Nitro?.Dispose();
            if (bodyMaterial) Destroy(bodyMaterial);
        }

        private void EnsureBindings()
        {
            if (Throttle == null) Throttle = new InputAction("Throttle", InputActionType.Value);
            if (BrakeReverse == null) BrakeReverse = new InputAction("BrakeReverse", InputActionType.Value);
            if (Steer == null) Steer = new InputAction("Steer", InputActionType.Value);
            if (Handbrake == null) Handbrake = new InputAction("Handbrake", InputActionType.Button);
            if (Nitro == null) Nitro = new InputAction("Nitro", InputActionType.Button);
            if (Throttle.bindings.Count == 0) { Throttle.AddBinding("<Keyboard>/w"); Throttle.AddBinding("<Keyboard>/upArrow"); Throttle.AddBinding("<Gamepad>/rightTrigger"); }
            if (BrakeReverse.bindings.Count == 0) { BrakeReverse.AddBinding("<Keyboard>/s"); BrakeReverse.AddBinding("<Keyboard>/downArrow"); BrakeReverse.AddBinding("<Gamepad>/leftTrigger"); }
            if (Steer.bindings.Count == 0)
            {
                Steer.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/a").With("Positive", "<Keyboard>/d");
                Steer.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/leftArrow").With("Positive", "<Keyboard>/rightArrow");
                Steer.AddBinding("<Gamepad>/leftStick/x");
            }
            if (Handbrake.bindings.Count == 0) { Handbrake.AddBinding("<Keyboard>/space"); Handbrake.AddBinding("<Gamepad>/buttonSouth"); }
            if (Nitro.bindings.Count == 0) { Nitro.AddBinding("<Keyboard>/leftShift"); Nitro.AddBinding("<Gamepad>/buttonEast"); }
        }

        /// <summary>Optional AI/replay/test input, using the same physics path as player input.</summary>
        public void SetExternalInput(DriverInput input) { externalInput = input; useExternalInput = true; }
        public void UsePlayerInput() => useExternalInput = false;
        public WheelContact GetWheelContact(int index) => index >= 0 && index < Wheels.Length ? Wheels[index].contact : default;
        private Vector3 Mount(Wheel wheel) => wheel.SuspensionPoint ? wheel.SuspensionPoint.position : transform.TransformPoint(wheel.LocalAnchor);

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            // Unity supplies the first 1g. Add only the extra acceleration to this body.
            if (Body.useGravity) Body.AddForce(Physics.gravity * (GravityMultiplier - 1), ForceMode.Acceleration);
            ApplyWallSlide(); preStepVelocity = Body.linearVelocity;
            SpeedKmh = Body.linearVelocity.magnitude * 3.6f;
            ForwardSpeed = Vector3.Dot(Body.linearVelocity, transform.forward);
            var input = useExternalInput ? externalInput : new DriverInput(Throttle.ReadValue<float>(), BrakeReverse.ReadValue<float>(),
                Steer.ReadValue<float>(), Handbrake.IsPressed(), Nitro.IsPressed());
            ThrottleInput = Mathf.Clamp01(input.Throttle); BrakeInput = Mathf.Clamp01(input.BrakeReverse);
            SteerInput = Mathf.MoveTowards(SteerInput, Mathf.Clamp(input.Steer, -1, 1), SteeringResponse * dt);
            HandbrakeInput = input.Handbrake;
            if (previousThrottle > .6f && ThrottleInput < .2f && Mathf.Abs(SteerInput) > .3f && SpeedKmh > 50) liftOffTimer = .7f;
            previousThrottle = ThrottleInput; liftOffTimer = Mathf.Max(0, liftOffTimer - dt);
            rearDriftBlend = Mathf.MoveTowards(rearDriftBlend, HandbrakeInput || liftOffTimer > 0 ? 1 : 0, dt * 4);
            float speedFraction = SpeedKmh / MaxSpeedKmh;
            float highSpeedSteering = Mathf.Lerp(1, HighSpeedSteeringScale,
                Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.65f, 1, speedFraction)));
            wheelSteerAngle = SteerInput * MaxSteerAngle * Mathf.Clamp(SteeringBySpeed.Evaluate(speedFraction), .05f, 1) * highSpeedSteering;
            if (UseWheelColliders) ReadWheelColliderContacts(); else CastSuspension();
            var planarVelocity = Vector3.ProjectOnPlane(Body.linearVelocity, groundNormal);
            SlipAngle = planarVelocity.sqrMagnitude > 1 ? Vector3.SignedAngle(CurrentGear < 0 ? -transform.forward : transform.forward, planarVelocity, groundNormal) : 0;
            IsDrifting = CurrentGear > 0 && GroundedWheelCount >= 2 && SpeedKmh > 30 && Mathf.Abs(SlipAngle) > DriftThreshold;
            UpdateTransmission(dt);
            IsNitroActive = input.Nitro && ThrottleInput > .1f && CurrentGear > 0 && GroundedWheelCount >= 2 && NitroAmount > NitroBurnRate * dt;
            if (IsNitroActive) NitroAmount = Mathf.Max(0, NitroAmount - NitroBurnRate * dt);
            else if (!input.Nitro && GroundedWheelCount >= 2 && SpeedKmh > 50)
                NitroAmount = Mathf.Min(NitroCapacity, NitroAmount + NitroRegenRate * (IsDrifting ? NitroDriftRegenMultiplier : 1) * dt);
            float driveInput = CurrentGear < 0 ? BrakeInput : ThrottleInput;
            bool oppositePedal = CurrentGear < 0 ? ThrottleInput > .01f : BrakeInput > .01f;
            float brake = oppositePedal ? (CurrentGear < 0 ? ThrottleInput : BrakeInput) : 0;
            float ratio = GearRatios[CurrentGear < 0 ? 0 : CurrentGear];
            float radius = (Wheels[2].Radius + Wheels[3].Radius) * .5f;
            float force = driveInput * EngineTorque * Mathf.Max(0, TorqueCurve.Evaluate(CurrentRPM / MaxRPM)) * ratio * FinalDrive / radius;
            if (oppositePedal || IsShifting) force *= IsShifting && !oppositePedal ? .15f : 0;
            float limit = (CurrentGear < 0 ? ReverseMaxSpeedKmh : MaxSpeedKmh + (IsNitroActive ? NitroMaxSpeedBonus : 0)) / 3.6f;
            force *= 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(limit * .97f, limit + 1, Mathf.Abs(ForwardSpeed)));
            if (UseWheelColliders) ApplyWheelColliderTyres(force, brake); else ApplyTyres(force, brake, dt);
            if (IsNitroActive) Body.AddForce(transform.forward * Mathf.Abs(force) * (NitroForceMultiplier - 1));
            if (GroundedWheelCount > 0)
            {
                Body.AddForce(-groundNormal * Body.linearVelocity.magnitude * DownforceFactor);
                if (planarVelocity.sqrMagnitude > .01f) Body.AddForce(-planarVelocity.normalized * Mass * RollingResistance);
                StabilizeDrift();
                if (IsDrifting && ThrottleInput > .1f && SpeedKmh < MaxSpeedKmh)
                    Body.AddForce(Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized * (Mass * DriftForwardBoost * ThrottleInput));
            }
            else
            {
                Vector3 tiltVelocity = Vector3.ProjectOnPlane(Body.angularVelocity, Vector3.up);
                // Angle-axis also works when the car is exactly upside down, where
                // Cross(up, worldUp) would be zero and could never right the body.
                Quaternion.FromToRotation(transform.up, Vector3.up).ToAngleAxis(out float angle, out Vector3 axis);
                if (angle > 180) angle -= 360;
                Body.AddTorque(axis * (angle * Mathf.Deg2Rad * AirLevelStrength) - tiltVelocity * AirAngularDamping, ForceMode.Acceleration);
            }
            Body.AddForce(-Body.linearVelocity * (Body.linearVelocity.magnitude * AeroDrag));
            if (Mathf.Abs(ForwardSpeed) > limit)
                Body.AddForce(-transform.forward * Mathf.Sign(ForwardSpeed) * ((Mathf.Abs(ForwardSpeed) - limit) * Mass * 8));
        }

        private void ReadWheelColliderContacts()
        {
            GroundedWheelCount = 0; Vector3 normals = Vector3.zero;
            foreach (var wheel in Wheels)
            {
                var collider = wheel.PhysicsCollider;
                wheel.contact = new WheelContact { Normal = transform.up, SpringLength = SuspensionRestLength + SuspensionTravel };
                if (!collider.GetGroundHit(out var hit) || !hit.collider || hit.collider.attachedRigidbody == Body) continue;
                collider.GetWorldPose(out var position, out _);
                float length = Vector3.Dot(Mount(wheel) - position, transform.up);
                wheel.contact = new WheelContact { Grounded = true, Point = hit.point, Normal = hit.normal, Collider = hit.collider,
                    SpringLength = length, Compression = Mathf.Max(0, SuspensionRestLength - length), NormalLoad = hit.force };
                GroundedWheelCount++; normals += hit.normal;
            }
            groundNormal = GroundedWheelCount > 0 ? normals.normalized : transform.up;
            ApplyAntiRoll(0, 1); ApplyAntiRoll(2, 3);
        }

        private void ApplyWheelColliderTyres(float driveForce, float brake)
        {
            int driven = 0; foreach (var wheel in Wheels) if (wheel.Driven) driven++;
            foreach (var wheel in Wheels)
            {
                var collider = wheel.PhysicsCollider;
                collider.steerAngle = wheel.Steers ? wheelSteerAngle : 0;
                collider.motorTorque = wheel.Driven ? driveForce * wheel.Radius / Mathf.Max(1, driven) : 0;
                float braking = Mathf.Max(brake * BrakeStrengthMultiplier, !wheel.Steers && HandbrakeInput ? 1 : 0);
                collider.brakeTorque = Mass * BrakeAcceleration * braking * wheel.Radius * .25f;
                var lateral = wheel.sidewaysFriction;
                if (!wheel.Steers) lateral.stiffness *= Mathf.Lerp(1, Mathf.Clamp01(DriftRearGrip / RearGrip), rearDriftBlend);
                collider.sidewaysFriction = lateral;
            }
        }

        private void CastSuspension()
        {
            GroundedWheelCount = 0; Vector3 normals = Vector3.zero;
            int mask = GroundLayers.value & ~(1 << VehicleLayer);
            foreach (var wheel in Wheels)
            {
                Vector3 origin = Mount(wheel), up = transform.up;
                float radius = Mathf.Min(CastRadius, wheel.Radius * .8f);
                float length = SuspensionRestLength + SuspensionTravel + wheel.Radius;
                int count = radius > .001f ? Physics.SphereCastNonAlloc(origin, radius, -up, castHits, length - radius, mask, QueryTriggerInteraction.Ignore) :
                    Physics.RaycastNonAlloc(origin, -up, castHits, length, mask, QueryTriggerInteraction.Ignore);
                float nearest = float.PositiveInfinity; RaycastHit hit = default; bool found = false;
                for (int i = 0; i < count; i++)
                {
                    var candidate = castHits[i];
                    if (candidate.collider.attachedRigidbody == Body || candidate.collider.transform.IsChildOf(transform) ||
                        Vector3.Dot(candidate.normal, up) < .4f || candidate.distance >= nearest) continue;
                    nearest = candidate.distance; hit = candidate; found = true;
                }
                wheel.contact = new WheelContact { SpringLength = SuspensionRestLength + SuspensionTravel, Normal = up };
                if (!found) continue;
                float springLength = Mathf.Clamp(hit.distance + radius - wheel.Radius, SuspensionRestLength - SuspensionTravel, SuspensionRestLength + SuspensionTravel);
                float compression = Mathf.Clamp(SuspensionRestLength - springLength, 0, SuspensionTravel);
                Vector3 relative = Body.GetPointVelocity(origin);
                if (hit.rigidbody) relative -= hit.rigidbody.GetPointVelocity(hit.point);
                float verticalVelocity = Vector3.Dot(relative, up);
                // Preserve static sag and damping ratio under the stronger vehicle gravity.
                float damping = DamperStiffness * Mathf.Sqrt(GravityMultiplier);
                float springForce = Mathf.Clamp(EffectiveSpringStiffness * compression - damping * verticalVelocity, 0, EffectiveSpringStiffness * SuspensionTravel * 3);
                Body.AddForceAtPosition(up * springForce, origin);
                if (hit.rigidbody && !hit.rigidbody.isKinematic) hit.rigidbody.AddForceAtPosition(-up * springForce, hit.point);
                wheel.contact = new WheelContact { Grounded = true, Point = hit.point, Normal = hit.normal, Collider = hit.collider,
                    SpringLength = springLength, Compression = compression, NormalLoad = springForce * Mathf.Max(0, Vector3.Dot(up, hit.normal)) };
                GroundedWheelCount++; normals += hit.normal;
            }
            groundNormal = GroundedWheelCount > 0 ? normals.normalized : Vector3.up;
            ApplyAntiRoll(0, 1); ApplyAntiRoll(2, 3);
        }

        private void ApplyAntiRoll(int left, int right)
        {
            if (!Wheels[left].contact.Grounded || !Wheels[right].contact.Grounded) return;
            float force = (Wheels[left].contact.Compression - Wheels[right].contact.Compression) * EffectiveSpringStiffness * AntiRoll;
            Body.AddForceAtPosition(transform.up * force, Mount(Wheels[left]));
            Body.AddForceAtPosition(-transform.up * force, Mount(Wheels[right]));
        }

        private void ApplyTyres(float driveForce, float brake, float dt)
        {
            int driven = 0; foreach (var wheel in Wheels) if (wheel.Driven && wheel.contact.Grounded) driven++;
            foreach (var wheel in Wheels)
            {
                var contact = wheel.contact; if (!contact.Grounded) continue;
                Vector3 forward = Vector3.ProjectOnPlane(Quaternion.AngleAxis(wheel.Steers ? wheelSteerAngle : 0, transform.up) * transform.forward, contact.Normal).normalized;
                Vector3 right = Vector3.Cross(contact.Normal, forward).normalized;
                Vector3 velocity = Body.GetPointVelocity(contact.Point);
                if (contact.Collider.attachedRigidbody) velocity -= contact.Collider.attachedRigidbody.GetPointVelocity(contact.Point);
                float grip = wheel.Steers ? FrontGrip : Mathf.Lerp(RearGrip, DriftRearGrip, rearDriftBlend);
                float lateral = -Vector3.Dot(velocity, right) * grip * Mass * .25f;
                float normalLimit = Mathf.Max(0, contact.NormalLoad) * TyreFriction;
                float lateralLimit = normalLimit * (wheel.Steers ? 1 : Mathf.Lerp(1, Mathf.Clamp01(DriftRearGrip / Mathf.Max(.1f, RearGrip)), rearDriftBlend));
                lateral = Mathf.Clamp(lateral, -lateralLimit, lateralLimit);
                // Extra gravity supplies braking grip, but must not turn into a free engine-traction boost.
                float driveLimit = normalLimit / GravityMultiplier;
                float longitudinal = wheel.Driven && driven > 0 ? Mathf.Clamp(driveForce / driven, -driveLimit, driveLimit) : 0;
                float wheelBrake = Mathf.Max(brake * BrakeStrengthMultiplier, !wheel.Steers && HandbrakeInput ? 1 : 0);
                if (wheelBrake > 0)
                {
                    float v = Vector3.Dot(velocity, forward);
                    longitudinal -= Mathf.Sign(v) * Mathf.Min(Mass * BrakeAcceleration * wheelBrake * .25f, Mathf.Abs(v) * Mass * .25f / dt);
                }
                longitudinal = Mathf.Clamp(longitudinal, -normalLimit, normalLimit);
                Vector3 tyreForce = Vector3.ClampMagnitude(forward * longitudinal + right * lateral, normalLimit);
                Body.AddForceAtPosition(tyreForce, contact.Point);
                if (contact.Collider.attachedRigidbody && !contact.Collider.attachedRigidbody.isKinematic)
                    contact.Collider.attachedRigidbody.AddForceAtPosition(-tyreForce, contact.Point);
            }
        }

        private void UpdateTransmission(float dt)
        {
            shiftTimer = Mathf.Max(0, shiftTimer - dt); shiftCooldown = Mathf.Max(0, shiftCooldown - dt);
            bool wantsOpposite = CurrentGear > 0 ? BrakeInput > .15f && ThrottleInput < .1f : ThrottleInput > .15f && BrakeInput < .1f;
            if (wantsOpposite && SpeedKmh < 2.2f) directionTimer += dt; else directionTimer = 0;
            if (directionTimer >= DirectionChangeDelay) { ChangeGear(CurrentGear > 0 ? -1 : 1); directionTimer = 0; }
            float radius = (Wheels[2].Radius + Wheels[3].Radius) * .5f;
            float ratio = Mathf.Abs(GearRatios[CurrentGear < 0 ? 0 : CurrentGear]);
            float wheelRPM = Mathf.Abs(ForwardSpeed) / (2 * Mathf.PI * radius) * 60 * ratio * FinalDrive;
            float pedal = CurrentGear < 0 ? BrakeInput : ThrottleInput;
            float targetRPM = Mathf.Clamp(Mathf.Max(wheelRPM, IdleRPM + pedal * 1700), IdleRPM, MaxRPM);
            CurrentRPM = Mathf.MoveTowards(CurrentRPM, targetRPM, dt * (targetRPM < CurrentRPM ? 22000 : 15000));
            if (CurrentGear <= 0 || IsShifting || shiftCooldown > 0) return;
            if (CurrentRPM >= ShiftUpRPM && CurrentGear < GearRatios.Length - 1) ChangeGear(CurrentGear + 1);
            else if (CurrentRPM < ShiftDownRPM && CurrentGear > 1)
            {
                float lowerRPM = wheelRPM * GearRatios[CurrentGear - 1] / ratio;
                if (lowerRPM < ShiftUpRPM - 500) ChangeGear(CurrentGear - 1);
            }
        }

        private void ChangeGear(int gear) { CurrentGear = gear; shiftTimer = ShiftDelay; shiftCooldown = ShiftDelay + .25f; }

        private void StabilizeDrift()
        {
            float yaw = Vector3.Dot(Body.angularVelocity, groundNormal);
            if (IsDrifting)
            {
                float targetSlip = -SteerInput * TargetDriftAngle;
                float desiredYaw = Mathf.Clamp((SlipAngle - targetSlip) * Mathf.Deg2Rad * 2.2f + SteerInput * .35f, -MaxYawRate, MaxYawRate);
                if (Mathf.Abs(SlipAngle) > MaxDriftAngle) desiredYaw = Mathf.Sign(SlipAngle) * MaxYawRate;
                Body.AddTorque(groundNormal * ((desiredYaw - yaw) * DriftAssistStrength), ForceMode.Acceleration);
            }
            float excess = yaw - Mathf.Clamp(yaw, -MaxYawRate, MaxYawRate);
            if (Mathf.Abs(excess) > .001f) Body.AddTorque(-groundNormal * excess, ForceMode.VelocityChange);
        }

        private void OnCollisionEnter(Collision collision) => RecordWall(collision);
        private void OnCollisionStay(Collision collision) => RecordWall(collision);
        private void RecordWall(Collision collision)
        {
            for (int i = 0; i < collision.contactCount; i++)
            {
                var contact = collision.GetContact(i);
                if (Mathf.Abs(Vector3.Dot(contact.normal, Vector3.up)) > .35f || preStepVelocity.magnitude < 8.33f) continue;
                float normalSpeed = Vector3.Dot(preStepVelocity, contact.normal);
                if (Mathf.Abs(normalSpeed) > preStepVelocity.magnitude * .65f) continue;
                WallPoint = contact.point; WallNormal = contact.normal; wallTime = Time.time;
                wallIncoming = preStepVelocity; pendingWall = true; break;
            }
        }

        private void ApplyWallSlide()
        {
            if (!pendingWall) return; pendingWall = false;
            Vector3 tangent = Vector3.ProjectOnPlane(wallIncoming, WallNormal);
            if (tangent.sqrMagnitude < 1) return;
            float lostAlongWall = Mathf.Clamp(tangent.magnitude - Vector3.Dot(Body.linearVelocity, tangent.normalized), 0, wallIncoming.magnitude);
            float inward = Mathf.Min(0, Vector3.Dot(Body.linearVelocity, WallNormal));
            Body.AddForce((tangent.normalized * lostAlongWall * WallSlideAssist - WallNormal * inward) * Mass, ForceMode.Impulse);
        }

        private void LateUpdate()
        {
            if (!Body) return;
            foreach (var wheel in Wheels)
            {
                if (!wheel.Visual) continue;
                if (UseWheelColliders)
                {
                    wheel.PhysicsCollider.GetWorldPose(out var position, out var rotation);
                    wheel.Visual.SetPositionAndRotation(position, rotation * wheel.visualBindRotation);
                    continue;
                }
                wheel.spin = Mathf.Repeat(wheel.spin + ForwardSpeed / wheel.Radius * Mathf.Rad2Deg * Time.deltaTime +
                    (wheel.Driven && IsBurningOut ? ThrottleInput * 600 * Time.deltaTime : 0), 360);
                wheel.Visual.position = Mount(wheel) - transform.up * wheel.contact.SpringLength;
                wheel.Visual.localRotation = wheel.visualBindRotation * Quaternion.Euler(0, wheel.Steers ? wheelSteerAngle : 0, 0) * Quaternion.Euler(wheel.spin, 0, 0);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (Wheels == null) return;
            foreach (var wheel in Wheels)
            {
                if (wheel == null) continue;
                Vector3 origin = Mount(wheel);
                Gizmos.color = !Application.isPlaying || !wheel.contact.Grounded ? Color.blue : wheel.contact.Compression >= SuspensionTravel * .95f ? Color.red : Color.green;
                Gizmos.DrawLine(origin, origin - transform.up * (SuspensionRestLength + SuspensionTravel + wheel.Radius));
                Gizmos.DrawWireSphere(origin, .05f);
                if (Application.isPlaying && wheel.contact.Grounded) Gizmos.DrawSphere(wheel.contact.Point, .065f);
            }
            if (Application.isPlaying && Body) { Gizmos.color = Color.cyan; Gizmos.DrawRay(Body.worldCenterOfMass, Body.linearVelocity * .15f); }
        }
    }
}
