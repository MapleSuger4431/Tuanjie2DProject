public class GameObject
{
    public string Name { get; set; }
    public Vector3 Position { get; set; }
    public Quaternion Rotation { get; set; }
    public Vector3 Scale { get; set; }

    public GameObject(string name)
    {
        Name = name;
        Position = new Vector3(0, 0, 0);
        Rotation = new Quaternion(0, 0, 0, 1);
        Scale = new Vector3(1, 1, 1);
    }

    public void Translate(Vector3 translation)
    {
        Position += translation;
    }

    public void Rotate(Quaternion rotation)
    {
        Rotation *= rotation;
    }

    public void ScaleObject(Vector3 scaleFactor)
    {
        Scale *= scaleFactor;
    }
}