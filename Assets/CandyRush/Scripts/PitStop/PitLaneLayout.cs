using UnityEngine;

namespace CandyRush.Integrated
{
    /// <summary>A mesma área define o piso rosa, a abertura das barreiras e a física do posto.</summary>
    public sealed class PitLaneLayout
    {
        readonly CircuitPath path;
        public readonly Vector3 Origin, Forward, Outward;
        public readonly float Inner, Outer, ServiceInner;
        public const float HalfLength = 27;
        public PitLaneLayout(CircuitPath path)
        {
            this.path=path;
            var sample=path.Samples[path.IndexAtFraction(CircuitConfig.PitAt)];
            Origin=sample.Position;
            Forward=new Vector3(sample.Tangent.x,0,sample.Tangent.z).normalized;
            Outward=sample.Left*CircuitConfig.PitSide;
            Inner=sample.HalfWidth-1;
            Outer=sample.HalfWidth+CircuitConfig.PitOffsetFromEdge-CircuitConfig.PitDepth/2;
            ServiceInner=sample.HalfWidth+10f;
        }
        public Vector2 Coordinates(float x,float z)
        {
            var delta=new Vector3(x-Origin.x,0,z-Origin.z);
            return new Vector2(Vector3.Dot(delta,Outward),Vector3.Dot(delta,Forward));
        }
        public bool Contains(float x,float z,float margin=0)
        {
            var p=Coordinates(x,z);
            return p.x>=Inner-margin && p.x<=Outer+margin && Mathf.Abs(p.y)<=HalfLength+margin;
        }
        public bool ReservesScenery(float x,float z,float margin=0)
        {
            var p=Coordinates(x,z);
            return p.x>=Inner-margin && p.x<=Outer+CircuitConfig.PitDepth+2+margin && Mathf.Abs(p.y)<=HalfLength+margin;
        }
        public Vector3 Point(float outward,float along,float height=.55f)
        {
            var p=Origin+Outward*outward+Forward*along;
            p.y=path.TerrainHeight(p.x,p.z)+height;
            return p;
        }
        public MeshData BuildPavement()
        {
            var mesh=new MeshData();
            int across=Mathf.CeilToInt(Outer-Inner),along=54;
            for(int z=0;z<=along;z++) for(int x=0;x<=across;x++)
            {
                var p=Point(Mathf.Lerp(Inner,Outer,x/(float)across),Mathf.Lerp(-HalfLength,HalfLength,z/(float)along),.018f);
                mesh.Add(p,Vector3.up,new Vector2(p.x,p.z)/CircuitConfig.TextureRepeatMeters);
            }
            for(int z=0;z<along;z++) for(int x=0;x<across;x++)
            {
                int a=z*(across+1)+x,b=a+1,c=a+across+1,d=c+1;
                mesh.Tri(a,c,b);mesh.Tri(b,c,d);
            }
            return mesh.ComputeNormals();
        }
    }
}
