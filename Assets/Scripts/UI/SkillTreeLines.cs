using UnityEngine;
using UnityEngine.UI;

/// <summary>Une los nodos reales de la escena. Mover un icono también actualiza sus conexiones.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public class SkillTreeLines : MaskableGraphic
{
    [Min(1f)] [SerializeField] private float thickness = 3f;
    private SkillNodeUI[] nodes;

    public void SetNodes(SkillNodeUI[] views)
    {
        nodes = views;
        SetVerticesDirty();
    }

    // Permite cambiar posiciones en Play sin volver a construir objetos ni romper las líneas.
    private void LateUpdate() => SetVerticesDirty();

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (nodes == null)
            return;
        foreach (SkillNodeUI node in nodes)
        {
            if (node == null || node.Skill == null || node.Skill.Prerequisite == null)
                continue;
            SkillNodeUI required = FindNode(node.Skill.Prerequisite);
            if (required == null)
                continue;
            Vector2 a = rectTransform.InverseTransformPoint(required.Rect.position);
            Vector2 b = rectTransform.InverseTransformPoint(node.Rect.position);
            Vector2 delta = b - a;
            if (delta.sqrMagnitude < 0.01f)
                continue;
            Vector2 normal = new Vector2(-delta.y, delta.x).normalized * thickness * 0.5f;
            int first = mesh.currentVertCount;
            AddVertex(mesh, a - normal);
            AddVertex(mesh, a + normal);
            AddVertex(mesh, b + normal);
            AddVertex(mesh, b - normal);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first, first + 2, first + 3);
        }
    }

    private SkillNodeUI FindNode(SkillDefinition skill)
    {
        foreach (SkillNodeUI node in nodes)
            if (node != null && node.Skill == skill)
                return node;
        return null;
    }

    private void AddVertex(VertexHelper mesh, Vector2 position)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = color;
        mesh.AddVert(vertex);
    }
}
