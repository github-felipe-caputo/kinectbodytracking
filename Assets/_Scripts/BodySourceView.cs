using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using Kinect = Windows.Kinect;
using System.Net.Sockets;
using System.Net;
using System.IO;
using UnityEngine.UI;

public class BodySourceView : MonoBehaviour 
{
    public Material BoneMaterial;
    public GameObject BodySourceManager;
    
    private BodySourceManager _BodyManager;
    
    private Dictionary<Kinect.JointType, Kinect.JointType> _BoneMap = new Dictionary<Kinect.JointType, Kinect.JointType>()
    {
        { Kinect.JointType.FootLeft, Kinect.JointType.AnkleLeft },
        { Kinect.JointType.AnkleLeft, Kinect.JointType.KneeLeft },
        { Kinect.JointType.KneeLeft, Kinect.JointType.HipLeft },
        { Kinect.JointType.HipLeft, Kinect.JointType.SpineBase },
        
        { Kinect.JointType.FootRight, Kinect.JointType.AnkleRight },
        { Kinect.JointType.AnkleRight, Kinect.JointType.KneeRight },
        { Kinect.JointType.KneeRight, Kinect.JointType.HipRight },
        { Kinect.JointType.HipRight, Kinect.JointType.SpineBase },
        
        { Kinect.JointType.HandTipLeft, Kinect.JointType.HandLeft },
        { Kinect.JointType.ThumbLeft, Kinect.JointType.HandLeft },
        { Kinect.JointType.HandLeft, Kinect.JointType.WristLeft },
        { Kinect.JointType.WristLeft, Kinect.JointType.ElbowLeft },
        { Kinect.JointType.ElbowLeft, Kinect.JointType.ShoulderLeft },
        { Kinect.JointType.ShoulderLeft, Kinect.JointType.SpineShoulder },
        
        { Kinect.JointType.HandTipRight, Kinect.JointType.HandRight },
        { Kinect.JointType.ThumbRight, Kinect.JointType.HandRight },
        { Kinect.JointType.HandRight, Kinect.JointType.WristRight },
        { Kinect.JointType.WristRight, Kinect.JointType.ElbowRight },
        { Kinect.JointType.ElbowRight, Kinect.JointType.ShoulderRight },
        { Kinect.JointType.ShoulderRight, Kinect.JointType.SpineShoulder },
        
        { Kinect.JointType.SpineBase, Kinect.JointType.SpineMid },
        { Kinect.JointType.SpineMid, Kinect.JointType.SpineShoulder },
        { Kinect.JointType.SpineShoulder, Kinect.JointType.Neck },
        { Kinect.JointType.Neck, Kinect.JointType.Head },
    };

    // body
    private GameObject bodyGameObject;

    // to store the data
    private TextWriter tw;
    private bool saveFileFlag = false;
    private float startTime;

    // to send the data
    private UdpClient client;
    private float[] bodyJoints = new float[25 * 3]; // joints to send
    private bool sendDataFlag = false;

    void Start() {
        bodyGameObject = CreateBodyObject();
    }

    void Update () 
    {
        if (BodySourceManager == null)
        {
            return;
        }
        
        _BodyManager = BodySourceManager.GetComponent<BodySourceManager>();
        if (_BodyManager == null)
        {
            return;
        }
        
        Kinect.Body[] data = _BodyManager.GetData();
        if (data == null)
        {
            return;
        }

        // For each body (we will only use one but it mught be body[0] or body[2], we dont know for sure)
        foreach (var body in data)
        {
            if (body == null) {
                continue;
            }

            if (body.IsTracked) {
                // refreshes how we show it in unity, won't really be necessary on the final version
                RefreshBodyObject(body, bodyGameObject);

                // need to send the joints, here is where I can grab the data,
                // put it on some float vector and udp it ->
                int i = 0;
                foreach (var joint in body.Joints) {
                    //print(joint.Key.ToString() + "  " + GetVector3FromJoint(joint.Value));

                    bodyJoints[i * 3 + 0] = joint.Value.Position.X;
                    bodyJoints[i * 3 + 1] = joint.Value.Position.Y;
                    bodyJoints[i * 3 + 2] = joint.Value.Position.Z;
                    i++;
                }

                // Store data to be reused!
                if (saveFileFlag)
                    saveToFile(bodyJoints);

                // send joints
                if (sendDataFlag)
                    UDPSend(bodyJoints);

            }
        }
    }

    public void OnButtonSaveToFileClicked() {
        if (saveFileFlag == false)
        {
            // If we are going to store, let's already create the file
            createFile( GameObject.Find("filename").GetComponent<Text>().text );

            GameObject.Find("SavingText").GetComponent<Text>().text = "Recording...";
            GameObject.Find("SaveButtonText").GetComponent<Text>().text = "Cancel Recording";

            saveFileFlag = true;
            startTime = Time.time;
        }
        else
        {
            saveFileFlag = false;
            tw.Close();
            GameObject.Find("SavingText").GetComponent<Text>().text = "Not Recording...";
            GameObject.Find("SaveButtonText").GetComponent<Text>().text = "Start Recording";
        }
    }

    public void OnButtonSendToClientClicked()
    {
        if (sendDataFlag == false)
        {
            // Start udp client
            client = new UdpClient();
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(GameObject.Find("IP").GetComponent<Text>().text), 15000); // 192.168.1.147 -> phone

            try
            {
                client.Connect(ep);
            }
            catch (Exception e)
            {
                Debug.Log("Error: " + e.ToString());
            }

            GameObject.Find("SendingText").GetComponent<Text>().text = "Sending...";
            GameObject.Find("SendButtonText").GetComponent<Text>().text = "Cancel Connection";

            sendDataFlag = true;
        }
        else {
            sendDataFlag = false;
            client.Close();
            GameObject.Find("SendingText").GetComponent<Text>().text = "Not Sending...";
            GameObject.Find("SendButtonText").GetComponent<Text>().text = "Send to Client";
        }
    }

    void OnApplicationQuit() {
        if (tw != null) {
            tw.Close();
        }

        if (client != null) {
            client.Close();
        }
    }

    private void createFile(string filename) {
        // will create a file if it doesnt exist, otherwise will append data to file
        tw = new StreamWriter(Application.dataPath + "/../" + filename, true);
    }

    private void saveToFile(float[] bodyJoints) {
        // first the time
        tw.Write((Time.time - startTime) + " ");

        for (int i = 0; i < bodyJoints.Length; ++i) {
            tw.Write(bodyJoints[i] + " ");
        } 
        tw.Write(Environment.NewLine);
    }

    private void UDPSend(float[] bodyjoints) {
        byte[] data = new byte[bodyjoints.Length * sizeof(float)]; // floats, times 4

        Buffer.BlockCopy(bodyjoints, 0, data, 0, bodyjoints.Length * sizeof(float)); // maybe * sizeof(float) here?     

        client.Send(data, data.Length);
    }

    private GameObject CreateBodyObject()
    {
        GameObject body = new GameObject();

        for (Kinect.JointType jt = Kinect.JointType.SpineBase; jt <= Kinect.JointType.ThumbRight; jt++)
        {
            GameObject jointObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            jointObj.GetComponent<Renderer>().material.shader = Shader.Find("Diffuse");

            LineRenderer lr = jointObj.AddComponent<LineRenderer>();
            lr.SetVertexCount(2);
            lr.material = BoneMaterial;
            lr.SetWidth(0.05f, 0.05f);

            jointObj.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            jointObj.name = jt.ToString();
            jointObj.transform.parent = body.transform;
        }

        return body;
    }
    
    private void RefreshBodyObject(Kinect.Body body, GameObject bodyObject)
    {
        for (Kinect.JointType jt = Kinect.JointType.SpineBase; jt <= Kinect.JointType.ThumbRight; jt++)
        {
            Kinect.Joint sourceJoint = body.Joints[jt];
            Kinect.Joint? targetJoint = null;
            
            if(_BoneMap.ContainsKey(jt))
            {
                targetJoint = body.Joints[_BoneMap[jt]];
            }
            
            Transform jointObj = bodyObject.transform.FindChild(jt.ToString());
            jointObj.localPosition = GetVector3FromJoint(sourceJoint);
            
            LineRenderer lr = jointObj.GetComponent<LineRenderer>();
            if(targetJoint.HasValue)
            {
                lr.SetPosition(0, jointObj.localPosition);
                lr.SetPosition(1, GetVector3FromJoint(targetJoint.Value));
                lr.SetColors(GetColorForState (sourceJoint.TrackingState), GetColorForState(targetJoint.Value.TrackingState));
            }
            else
            {
                lr.enabled = false;
            }
        }
    }
    
    private static Color GetColorForState(Kinect.TrackingState state)
    {
        switch (state)
        {
        case Kinect.TrackingState.Tracked:
            return Color.green;

        case Kinect.TrackingState.Inferred:
            return Color.red;

        default:
            return Color.black;
        }
    }
    
    private static Vector3 GetVector3FromJoint(Kinect.Joint joint)
    {
        return new Vector3(joint.Position.X * 10, joint.Position.Y * 10, joint.Position.Z * 10);
    }
}
