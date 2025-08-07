// 7/30/2025 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

using UnityEngine;
using UnityEngine.InputSystem;

public class BolaCollisionDetector : MonoBehaviour
{
    // Acción de entrada para el clic del mouse
    public InputAction mouseClickAction;

    private void OnEnable()
    {
        // Habilitar la acción de entrada
        mouseClickAction.Enable();
        mouseClickAction.performed += OnMouseClick;
    }

    private void OnDisable()
    {
        // Deshabilitar la acción de entrada
        mouseClickAction.performed -= OnMouseClick;
        mouseClickAction.Disable();
    }

    private void OnMouseClick(InputAction.CallbackContext context)
    {
        // Obtener la posición del mouse
        Vector2 mousePosition = Mouse.current.position.ReadValue();

        // Crear un rayo desde la cámara hacia la posición del mouse
        Ray ray = Camera.main.ScreenPointToRay(mousePosition);
        RaycastHit hit;

        // Realizar el raycast
        if (Physics.Raycast(ray, out hit))
        {
            // Mostrar en consola el nombre del objeto impactado
            Debug.Log("Objeto impactado: " + hit.collider.gameObject.name);
        }
        else
        {
            // Si no se impacta ningún objeto
            Debug.Log("No se impactó ningún objeto.");
        }
    }
}