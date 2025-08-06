using UnityEngine;

public class ClickEsfera : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }
    void OnMouseDown()
    {
        GameController.instance.EsferaClickeada(this.gameObject); // Detecta el clic en la esfera y llama al método EsferaClickeada
       
       
    }
}
