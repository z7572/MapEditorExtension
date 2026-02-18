using LevelEditor;
using UnityEngine;

public class CheckPreRotation : MonoBehaviour
{
    public Vector3 preRotationAngle = Vector3.zero;

    private void Start()
    {
        var objName = transform.name;
        if (objName == "BRUSHOBJECT" || objName == "BRUSHOBJECT_MIRROR")
        {
            if (transform.childCount == 0) return;
            objName = transform.GetChild(0).name;
        }

        switch (objName)
        {
            // SNAKE BARREL
            case "SnakeBarrel_LevelEditor":
            {
                preRotationAngle = new Vector3(90f, 0f, 0f);
                break;
            }

            // SPIKE
            case "Spike":
            {
                preRotationAngle = new Vector3(90f, 0f, 0f);
                break;
            }

            // HINGE STEP
            case "Castle_Platform1 (1)":
            {
                preRotationAngle = new Vector3(90f, 0f, 0f);
                break;
            }

            // ICE, SMALL ICE, etc.
            default:
            {
                if (transform.rotation.x != 0f)
                {
                    preRotationAngle = transform.rotation.eulerAngles;
                }
                break;
            }
        }
    }
}