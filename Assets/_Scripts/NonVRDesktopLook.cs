using UnityEngine;

/// <summary>
/// Optional mouse-look for the non-VR fallback camera during gameplay.
/// Hold right mouse to rotate; Esc releases the cursor. Disabled while in the menu.
/// </summary>
public class NonVRDesktopLook : MonoBehaviour
{
    public float sensitivity = 2f;
    public bool requireRightMouseButton = true;

    bool lookEnabled;

    float pitch;
    float yaw;

    public void SetLookEnabled(bool enabled)
    {
        lookEnabled = enabled;
        if (!lookEnabled && Cursor.lockState == CursorLockMode.Locked)
            Cursor.lockState = CursorLockMode.None;
        if (lookEnabled)
            SyncFromTransform();
    }

    public void SyncFromTransform()
    {
        Vector3 e = transform.eulerAngles;
        yaw = e.y;
        pitch = e.x;
        if (pitch > 180f) pitch -= 360f;
    }

    void Start()
    {
        SyncFromTransform();
    }

    void Update()
    {
        if (!lookEnabled)
            return;

        if (requireRightMouseButton && !Input.GetMouseButton(1))
        {
            if (Cursor.lockState == CursorLockMode.Locked)
                Cursor.lockState = CursorLockMode.None;
            return;
        }

        if (requireRightMouseButton && Input.GetMouseButtonDown(1))
            Cursor.lockState = CursorLockMode.Locked;

        if (Input.GetKeyDown(KeyCode.Escape))
            Cursor.lockState = CursorLockMode.None;

        float mx = Input.GetAxis("Mouse X") * sensitivity;
        float my = Input.GetAxis("Mouse Y") * sensitivity;
        yaw += mx;
        pitch -= my;
        pitch = Mathf.Clamp(pitch, -89f, 89f);
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }
}
