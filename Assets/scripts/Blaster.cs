using UnityEngine;

public class Blaster : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [SerializeField]
    private OVRInput.RawButton botonDisparo;
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (OVRInput.GetDown(botonDisparo))
        {
            Shoot();
        }
    }
    void Shoot()
    {
        if(GameController.Instance != null)
        {
            GameController.Instance.MostrarTextoReloj("fuck");
        }
    }
}
