using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SimulationUIController : MonoBehaviour
{
    [Header("Simulation References")]
    public SphericalPendulumController pendulumController;
    public MassSpringRope ropeController;
    public BucketSimulation3D bucketFluidSimulation;

    [Header("Sliders - Pendulum")]
    public Slider ropeLengthSlider;
    public Slider initialAngleSlider;
    public Slider sidePushSlider;
    public Slider dampingSlider;
    public Slider gravitySlider;

    [Header("Sliders - Paint")]
    public Slider emissionRateSlider;
    public Slider exitSpeedSlider;
    public Slider viscositySlider;
    public Slider holeDiameterSlider;

    [Header("Value Texts - Pendulum")]
    public TMP_Text ropeLengthValueText;
    public TMP_Text initialAngleValueText;
    public TMP_Text sidePushValueText;
    public TMP_Text dampingValueText;
    public TMP_Text gravityValueText;

    [Header("Value Texts - Paint")]
    public TMP_Text emissionRateValueText;
    public TMP_Text exitSpeedValueText;
    public TMP_Text viscosityValueText;
    public TMP_Text holeDiameterValueText;

    [Header("Info Texts")]
    public TMP_Text timeText;
    public TMP_Text bucketSpeedText;
    public TMP_Text swingCountText;
    public TMP_Text paintRemainingText;

    [Header("Physical Output Texts")]
    public TMP_Text totalMassText;
    public TMP_Text ropeTensionText;
    public TMP_Text effectiveDampingText;
    public TMP_Text kineticEnergyText;
    public TMP_Text potentialEnergyText;

    private float simulationTime = 0f;
    private bool isPaused = true;

    [Header("Paint Values")]
    public float emissionRate = 120f;
    public float exitSpeed = 3f;
    public float viscosity = 0.5f;
    public float holeDiameter = 0.08f;

    private int swingCount = 0;
    private bool swingCounterReady = false;
    private float previousRadialVelocity = 0f;

    private void Start()
    {
        InitializeSliders();
        ConnectSliders();
        UpdateValueTexts();

        simulationTime = 0f;
        swingCount = 0;
        swingCounterReady = false;

        isPaused = true;
        Time.timeScale = 0f;

        UpdateInfoTexts();
    }

    private void Update()
    {
        if (!isPaused)
        {
            simulationTime += Time.unscaledDeltaTime;
            UpdateSwingCount();
        }

        UpdateInfoTexts();
    }

    private void InitializeSliders()
    {
        if (pendulumController == null)
        {
            Debug.LogWarning("Pendulum Controller is not assigned.");
            return;
        }

        if (ropeLengthSlider != null)
            ropeLengthSlider.value = pendulumController.ropeLength;

        if (initialAngleSlider != null)
            initialAngleSlider.value = pendulumController.theta * Mathf.Rad2Deg;

        if (sidePushSlider != null)
            sidePushSlider.value = pendulumController.phiDot;

        if (dampingSlider != null)
            dampingSlider.value = pendulumController.damping;

        if (gravitySlider != null)
            gravitySlider.value = pendulumController.gravity;

        if (emissionRateSlider != null)
            emissionRateSlider.value = emissionRate;

        if (exitSpeedSlider != null)
            exitSpeedSlider.value = exitSpeed;

        if (viscositySlider != null)
            viscositySlider.value = viscosity;

        if (holeDiameterSlider != null)
            holeDiameterSlider.value = holeDiameter;
    }

    private void ConnectSliders()
    {
        if (ropeLengthSlider != null)
            ropeLengthSlider.onValueChanged.AddListener(ChangeRopeLength);

        if (initialAngleSlider != null)
            initialAngleSlider.onValueChanged.AddListener(ChangeInitialAngle);

        if (sidePushSlider != null)
            sidePushSlider.onValueChanged.AddListener(ChangeSidePush);

        if (dampingSlider != null)
            dampingSlider.onValueChanged.AddListener(ChangeDamping);

        if (gravitySlider != null)
            gravitySlider.onValueChanged.AddListener(ChangeGravity);

        if (emissionRateSlider != null)
            emissionRateSlider.onValueChanged.AddListener(ChangeEmissionRate);

        if (exitSpeedSlider != null)
            exitSpeedSlider.onValueChanged.AddListener(ChangeExitSpeed);

        if (viscositySlider != null)
            viscositySlider.onValueChanged.AddListener(ChangeViscosity);

        if (holeDiameterSlider != null)
            holeDiameterSlider.onValueChanged.AddListener(ChangeHoleDiameter);
    }

    private void ChangeRopeLength(float value)
    {
        if (pendulumController != null)
        {
            pendulumController.ropeLength = value;
            pendulumController.ResetPendulum();
        }

        if (ropeController != null)
        {
            ropeController.ropeLength = value + 0.08f;
            ropeController.ResetRope();
        }

        ResetCountersOnly();
        UpdateValueTexts();
        UpdateInfoTexts();
    }

    private void ChangeInitialAngle(float value)
    {
        if (pendulumController != null)
        {
            pendulumController.theta = value * Mathf.Deg2Rad;
            pendulumController.ResetPendulum();
        }

        if (ropeController != null)
        {
            ropeController.ResetRope();
        }

        ResetCountersOnly();
        UpdateValueTexts();
        UpdateInfoTexts();
    }

    private void ChangeSidePush(float value)
    {
        if (pendulumController != null)
        {
            pendulumController.phiDot = value;
            pendulumController.ResetPendulum();
        }

        if (ropeController != null)
        {
            ropeController.ResetRope();
        }

        ResetCountersOnly();
        UpdateValueTexts();
        UpdateInfoTexts();
    }

    private void ChangeDamping(float value)
    {
        if (pendulumController != null)
        {
            pendulumController.damping = value;
        }

        UpdateValueTexts();
        UpdateInfoTexts();
    }

    private void ChangeGravity(float value)
    {
        if (pendulumController != null)
        {
            pendulumController.gravity = value;
            pendulumController.ResetPendulum();
        }

        if (ropeController != null)
        {
            ropeController.gravity = new Vector3(0f, -value, 0f);
            ropeController.ResetRope();
        }

        ResetCountersOnly();
        UpdateValueTexts();
        UpdateInfoTexts();
    }

    private void ChangeEmissionRate(float value)
    {
        emissionRate = value;
        UpdateValueTexts();
    }

    private void ChangeExitSpeed(float value)
    {
        exitSpeed = value;
        UpdateValueTexts();
    }

    private void ChangeViscosity(float value)
    {
        viscosity = value;
        UpdateValueTexts();
    }

    private void ChangeHoleDiameter(float value)
    {
        holeDiameter = value;
        UpdateValueTexts();
    }

    private void UpdateValueTexts()
    {
        if (ropeLengthValueText != null && ropeLengthSlider != null)
            ropeLengthValueText.text = ropeLengthSlider.value.ToString("0.00") + " m";

        if (initialAngleValueText != null && initialAngleSlider != null)
            initialAngleValueText.text = initialAngleSlider.value.ToString("0") + "°";

        if (sidePushValueText != null && sidePushSlider != null)
            sidePushValueText.text = sidePushSlider.value.ToString("0.00");

        if (dampingValueText != null && dampingSlider != null)
            dampingValueText.text = dampingSlider.value.ToString("0.000");

        if (gravityValueText != null && gravitySlider != null)
            gravityValueText.text = gravitySlider.value.ToString("0.00") + " m/s²";

        if (emissionRateValueText != null && emissionRateSlider != null)
            emissionRateValueText.text = emissionRateSlider.value.ToString("0") + " particles/s";

        if (exitSpeedValueText != null && exitSpeedSlider != null)
            exitSpeedValueText.text = exitSpeedSlider.value.ToString("0.00") + " m/s";

        if (viscosityValueText != null && viscositySlider != null)
            viscosityValueText.text = viscositySlider.value.ToString("0.00");

        if (holeDiameterValueText != null && holeDiameterSlider != null)
            holeDiameterValueText.text = holeDiameterSlider.value.ToString("0.00") + " m";
    }

    private void UpdateInfoTexts()
    {
        if (timeText != null)
            timeText.text = "Time: " + simulationTime.ToString("0.0") + " s";

        if (bucketSpeedText != null && pendulumController != null)
        {
            float speed = pendulumController.attachPointVelocity.magnitude;
            bucketSpeedText.text = "Bucket Speed: " + speed.ToString("0.00") + " m/s";
        }

        if (swingCountText != null)
            swingCountText.text = "Swing Count: " + swingCount;

        if (paintRemainingText != null)
            paintRemainingText.text = "Paint Remaining: 100%";

        if (pendulumController != null)
        {
            if (totalMassText != null)
                totalMassText.text = "Total Mass: " + pendulumController.totalMass.ToString("0.00") + " kg";

            if (ropeTensionText != null)
                ropeTensionText.text = "Rope Tension: " + pendulumController.ropeTension.ToString("0.00") + " N";

            if (effectiveDampingText != null)
                effectiveDampingText.text = "Effective Damping: " + pendulumController.effectiveDamping.ToString("0.000");

            if (kineticEnergyText != null)
                kineticEnergyText.text = "Kinetic Energy: " + pendulumController.kineticEnergy.ToString("0.00") + " J";

            if (potentialEnergyText != null)
                potentialEnergyText.text = "Potential Energy: " + pendulumController.potentialEnergy.ToString("0.00") + " J";
        }
    }

    private void UpdateSwingCount()
    {
        if (pendulumController == null || pendulumController.ropeAttachPoint == null || pendulumController.pivotPoint == null)
        {
            return;
        }

        Vector3 fromPivot = pendulumController.ropeAttachPoint.position - pendulumController.pivotPoint.position;

        Vector3 horizontalDisplacement = new Vector3(fromPivot.x, 0f, fromPivot.z);
        Vector3 horizontalVelocity = new Vector3(
            pendulumController.attachPointVelocity.x,
            0f,
            pendulumController.attachPointVelocity.z
        );

        if (horizontalDisplacement.magnitude < 0.05f)
        {
            return;
        }

        float radialVelocity = Vector3.Dot(horizontalDisplacement.normalized, horizontalVelocity);

        if (!swingCounterReady)
        {
            previousRadialVelocity = radialVelocity;
            swingCounterReady = true;
            return;
        }

        /*
         * عندما تتحول السرعة الشعاعية من موجبة إلى سالبة،
         * فهذا يعني أن الدلو وصل لطرف المسار وبدأ يرجع.
         */
        bool reachedOuterTurn =
            previousRadialVelocity > 0.02f &&
            radialVelocity <= -0.02f;

        if (reachedOuterTurn && simulationTime > 0.2f)
        {
            swingCount++;
        }

        previousRadialVelocity = radialVelocity;
    }

    public void StartSimulation()
    {
        isPaused = false;
        Time.timeScale = 1f;
    }

    public void PauseSimulation()
    {
        isPaused = true;
        Time.timeScale = 0f;
    }

    public void ResetSimulation()
    {
        simulationTime = 0f;
        swingCount = 0;
        swingCounterReady = false;
        previousRadialVelocity = 0f;

        if (pendulumController != null)
        {
            pendulumController.ResetPendulum();
        }

        if (ropeController != null)
        {
            ropeController.ResetRope();
        }

        if (bucketFluidSimulation != null)
        {
            bucketFluidSimulation.ResetFluid();
        }

        UpdateValueTexts();
        UpdateInfoTexts();

        isPaused = true;
        Time.timeScale = 0f;
    }

    private void ResetCountersOnly()
    {
        simulationTime = 0f;
        swingCount = 0;
        swingCounterReady = false;
        previousRadialVelocity = 0f;
    }

    private void OnDisable()
    {
        Time.timeScale = 1f;
    }
}