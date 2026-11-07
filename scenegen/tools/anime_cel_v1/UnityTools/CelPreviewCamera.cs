using UnityEngine;

// Desktop visual inspection only. The consuming game supplies its own movement and camera tracking.
public class CelPreviewCamera : MonoBehaviour
{
    float yaw,pitch;
    void Start() { yaw=transform.eulerAngles.y; pitch=transform.eulerAngles.x; }
    void Update()
    {
        if(Input.GetMouseButton(1))
        {
            yaw+=Input.GetAxis("Mouse X")*2; pitch=Mathf.Clamp(pitch-Input.GetAxis("Mouse Y")*2,-80,80);
            transform.rotation=Quaternion.Euler(pitch,yaw,0);
        }
        float speed=Input.GetKey(KeyCode.LeftShift)?6:2;
        Vector3 v=new Vector3(Input.GetAxisRaw("Horizontal"),0,Input.GetAxisRaw("Vertical"));
        if(Input.GetKey(KeyCode.E)) v.y+=1; if(Input.GetKey(KeyCode.Q)) v.y-=1;
        transform.position+=transform.TransformDirection(v)*speed*Time.deltaTime;
    }
}
