using UnityEngine;

// Resaltado de una esfera del tablero mientras el láser de puntería está sobre ella.
// Blaster lo agrega en tiempo de ejecución. Lleva la cuenta de cuántos blasters la
// apuntan para que, con ambos controles sobre la misma esfera, la escala no se acumule.
public class ResaltadoEsfera : MonoBehaviour
{
    private Vector3 escalaBase;
    private int punterosEncima = 0;

    void Awake()
    {
        escalaBase = transform.localScale;
    }

    public void Entrar(float factorEscala)
    {
        punterosEncima++;
        transform.localScale = escalaBase * factorEscala;
    }

    public void Salir()
    {
        punterosEncima = Mathf.Max(0, punterosEncima - 1);
        if (punterosEncima == 0)
        {
            transform.localScale = escalaBase;
        }
    }
}
