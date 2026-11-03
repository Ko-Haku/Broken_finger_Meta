using UnityEngine;

public class BlackPanelBehaviour : MonoBehaviour
{
    public Animator animator;
    public bool is_Panel_Open = false;

    // Compatibilità: se lo usi ancora da qualche parte, resta.
    public void RuotaPannello()
    {
        if (!is_Panel_Open) OpenPanel();
        else ClosePanel();
    }

    public void OpenPanel()
    {
        if (is_Panel_Open) return;
        animator.SetBool("ruota", true);
        is_Panel_Open = true;
    }

    public void ClosePanel()
    {
        if (!is_Panel_Open) return;
        animator.SetBool("ruota", false);
        is_Panel_Open = false;
    }

    // Se non ti serve più la tastiera, puoi anche rimuovere Update().
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.A))
            RuotaPannello();
    }
}