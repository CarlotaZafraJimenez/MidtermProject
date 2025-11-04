using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class Tile : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
{
    public Vector2Int gridPosition;

    [Header("Colors")]
    public Color originalColor;
    public Color highlightColor = Color.yellow;
    public Color selectedColor = Color.blue;

    private Renderer tileRenderer;
    public static Tile selectedTile;

    public bool inMoveRange = false;
    public bool inAttackRange = false;
    public int moveCost = 0;
    public bool isOccupied = false;

    public int gCost = int.MaxValue;
    public int hCost = 0;
    public int fCost => gCost + hCost;
    public Tile parent;

    private void Start()
    {
        tileRenderer = GetComponentInChildren<Renderer>();
        ChangeColor(originalColor);
    }

    // Cambia el color del tile
    public void ChangeColor(Color newColor)
    {
        tileRenderer.material.color = newColor;
    }

    // Se activa al pasar el ratón por encima
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (selectedTile != this)
        {
            ChangeColor(highlightColor);
        }
    }

    // Se activa al salir el ratón del tile
    public void OnPointerExit(PointerEventData eventData)
    {
        if (selectedTile != this)
        {
            ChangeColor(inMoveRange ? Color.cyan : inAttackRange ? Color.red : originalColor);
        }
    }

    // Se activa al hacer clic en el tile
    public void OnPointerDown(PointerEventData eventData)
    {
        GridManager gridManager = GetComponentInParent<GridManager>();

        if (Player.selectedUnit)
        {
            // No permitir si la unidad ya se está moviendo
            if (Player.selectedUnit.isMoving) return;

            if (inMoveRange)
            {
                Tile startTile = gridManager.GetTile(Player.selectedUnit.gridPosition);
                List<Tile> path = gridManager.GetPath(startTile, this);

                if (path != null && path.Count > 0)
                {
                    Player.selectedUnit.MoveTo(path); // ✅ Usa la nueva firma
                }
            }
            else
            {
                Player.selectedUnit = null;
            }
        }

        SelectTile();
        gridManager.ResetGridHighlights();
    }

    // Selecciona este tile visualmente
    public void SelectTile()
    {
        if (selectedTile)
        {
            selectedTile.ChangeColor(selectedTile.originalColor);
        }

        selectedTile = this;
        ChangeColor(selectedColor);
    }
}
