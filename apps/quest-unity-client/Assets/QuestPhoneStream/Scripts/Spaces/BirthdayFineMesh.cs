using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuestPhoneStream.Spaces
{
    /// <summary>
    /// Lightweight decorative geometry for mobile/Quest. All geometry shares three
    /// mesh material slots rather than spawning individual spheres as petals.
    /// Geometry is authored in meters and never has colliders or realtime shadows.
    /// </summary>
    public static class BirthdayFineMesh
    {
        sealed class Builder
        {
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<int>[] indices;
            public Builder(int materialCount)
            {
                indices = new List<int>[materialCount];
                for (int i = 0; i < materialCount; i++) indices[i] = new List<int>();
            }
            public void Triangle(int material, Vector3 a, Vector3 b, Vector3 c)
            {
                int v = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                indices[material].Add(v); indices[material].Add(v+1); indices[material].Add(v+2);
            }
            public void Quad(int material, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                Triangle(material, a,b,c);
                Triangle(material,a,c,d);
            }
            public void Box(int material, Vector3 center, Vector3 half)
            {
                var a=center-half; var b=center+half;
                Quad(material,new Vector3(a.x,a.y,a.z),new Vector3(b.x,a.y,a.z),
                     new Vector3(b.x,b.y,a.z),new Vector3(a.x,b.y,a.z));
                Quad(material,new Vector3(b.x,a.y,b.z),new Vector3(a.x,a.y,b.z),
                     new Vector3(a.x,b.y,b.z),new Vector3(b.x,b.y,b.z));
                Quad(material,new Vector3(a.x,a.y,b.z),new Vector3(a.x,a.y,a.z),
                     new Vector3(a.x,b.y,a.z),new Vector3(a.x,b.y,b.z));
                Quad(material,new Vector3(b.x,a.y,a.z),new Vector3(b.x,a.y,b.z),
                     new Vector3(b.x,b.y,b.z),new Vector3(b.x,b.y,a.z));
                Quad(material,new Vector3(a.x,b.y,a.z),new Vector3(b.x,b.y,a.z),
                     new Vector3(b.x,b.y,b.z),new Vector3(a.x,b.y,b.z));
                Quad(material,new Vector3(a.x,a.y,b.z),new Vector3(b.x,a.y,b.z),
                     new Vector3(b.x,a.y,a.z),new Vector3(a.x,a.y,a.z));
            }
            public Mesh Finish(string name)
            {
                var mesh=new Mesh {name=name};
                mesh.SetVertices(vertices);
                mesh.subMeshCount=indices.Length;
                for(int i=0;i<indices.Length;i++)mesh.SetTriangles(indices[i],i);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }
        public static Mesh Bouquet(float height)
        {
            var b=new Builder(3); // blush, ivory, leaves
            for(int flower=0;flower<5;flower++)
            {
                float a=flower*1.256637f;
                Vector3 ring=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*(height*.24f);
                Vector3 center=ring+Vector3.up*height*(.81f+.05f*(flower%2));
                Vector3 stem=Vector3.up*(height*.26f)+ring*.10f;
                // Fine tapered greenery and two asymmetric, folded leaf blades.
                Vector3 axis=center-stem;
                Vector3 cross=Vector3.Cross(axis.normalized,Vector3.forward).normalized;
                if(cross.sqrMagnitude<.01f)cross=Vector3.right;
                b.Quad(2,stem-cross*height*.012f,stem+cross*height*.012f,
                    center+cross*height*.007f,center-cross*height*.007f);
                for(int leaf=0;leaf<2;leaf++)
                {
                    float dir=leaf==0?-1f:1f;
                    Vector3 basePt=stem+axis*(.35f+leaf*.17f);
                    Vector3 edge=Vector3.Cross(axis.normalized,Vector3.up).normalized;
                    if(edge.sqrMagnitude<.01f)edge=Vector3.forward;
                    edge*=dir*height*.17f;
                    b.Triangle(2,basePt,basePt+edge*.55f+Vector3.up*height*.025f,
                        basePt+axis*.28f);
                    b.Triangle(2,basePt+edge*.55f+Vector3.up*height*.025f,
                        basePt+edge+Vector3.up*height*.04f,basePt+axis*.28f);
                }
                // Two offset rings of curled, overlapping rose petals.
                float r=height*.145f;
                for(int ringIndex=0;ringIndex<2;ringIndex++)
                {
                    int petalCount=ringIndex==0?7:5;
                    float radius=ringIndex==0?r:r*.67f;
                    for(int petal=0;petal<petalCount;petal++)
                    {
                        float theta=(petal+(ringIndex==1?.42f:0f))*Mathf.PI*2/petalCount;
                        Vector3 radial=new Vector3(Mathf.Cos(theta),0,Mathf.Sin(theta));
                        Vector3 lateral=new Vector3(-radial.z,0,radial.x);
                        float width=radius*(ringIndex==0?.79f:.65f);
                        Vector3 basePt=center+radial*radius*.27f-Vector3.up*radius*.42f;
                        Vector3 left=center+radial*radius*.94f-lateral*width+Vector3.up*radius*.09f;
                        Vector3 tip=center+radial*radius*1.36f+Vector3.up*radius*.68f;
                        Vector3 right=center+radial*radius*.94f+lateral*width+Vector3.up*radius*.09f;
                        int material=(flower%2==0)?0:1;
                        b.Triangle(material,basePt,left,tip);
                        b.Triangle(material,basePt,tip,right);
                    }
                }
                // Folded bud: petals, not round primitive golden beads.
                for(int petal=0;petal<5;petal++)
                {
                    float theta=petal*Mathf.PI*2/5;
                    Vector3 radial=new Vector3(Mathf.Cos(theta),0,Mathf.Sin(theta));
                    b.Triangle(1,center+Vector3.up*r*.30f,
                        center+radial*r*.33f+Vector3.up*r*.16f,
                        center+Vector3.up*r*.94f);
                }
            }
            return b.Finish("Birthday_Bouquet_FoldedPetals");
        }
        public static Mesh CakeIcing(float radius, float y)
        {
            var b=new Builder(1);
            const int segments=48;
            for(int i=0;i<segments;i++)
            {
                float a=i*2f*Mathf.PI/segments, next=(i+1)*2f*Mathf.PI/segments;
                Vector3 x=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                Vector3 x2=new Vector3(Mathf.Cos(next),0,Mathf.Sin(next));
                float drip=.016f+.010f*(.5f+.5f*Mathf.Sin(i*2f*Mathf.PI/6f));
                float drip2=.016f+.010f*(.5f+.5f*Mathf.Sin((i+1)*2f*Mathf.PI/6f));
                b.Quad(0,x*(radius-.020f)+Vector3.up*(y+.010f),
                    x2*(radius-.020f)+Vector3.up*(y+.010f),
                    x2*radius+Vector3.up*y,x*radius+Vector3.up*y);
                b.Quad(0,x*radius+Vector3.up*y,x2*radius+Vector3.up*y,
                    x2*radius+Vector3.up*(y-drip2),x*radius+Vector3.up*(y-drip));
            }
            return b.Finish("Birthday_CakeScallopedIcing");
        }
        public static Mesh CandleTrio()
        {
            var b=new Builder(2); // white wax, golden flame
            for(int i=0;i<3;i++)
            {
                float x=(i-1)*.075f;
                float h=i==1?.095f:.072f;
                float y=.260f;
                b.Box(0,new Vector3(x,y+h*.5f,0),new Vector3(.009f,h*.5f,.009f));
                var bottom=new Vector3(x,y+h+.006f,0);
                b.Triangle(1,bottom+Vector3.right*.009f,bottom+Vector3.forward*.009f,
                    bottom+Vector3.up*.035f);
                b.Triangle(1,bottom+Vector3.left*.009f,bottom-Vector3.forward*.009f,
                    bottom+Vector3.up*.035f);
            }
            return b.Finish("Birthday_Candles_ThreeWarmFlames");
        }
        public static Mesh FrameMoulding(float width,float height)
        {
            var b=new Builder(1);
            float side=width*.08f,upper=height*.07f;
            // Negative X is viewer-facing for west-facing gallery.
            b.Box(0,new Vector3(-.032f,height*.5f,0),new Vector3(.014f,upper*.5f,width*.5f));
            b.Box(0,new Vector3(-.032f,-height*.5f,0),new Vector3(.014f,upper*.5f,width*.5f));
            b.Box(0,new Vector3(-.032f,0,-width*.5f),new Vector3(.014f,height*.5f,side*.5f));
            b.Box(0,new Vector3(-.032f,0,width*.5f),new Vector3(.014f,height*.5f,side*.5f));
            return b.Finish("Birthday_PhotoFrame_RealMoulding");
        }
        public static Mesh BalloonStrings(Vector3[] points)
        {
            var b=new Builder(1);
            for(int j=0;j<points.Length;j++)
            {
                var p=points[j];
                float knotDrop=.132f+(j%3)*.017f;
                for(int n=0;n<5;n++)
                {
                    float t=n/5f,next=(n+1)/5f;
                    Func<float,Vector3> at=u=>p+new Vector3(
                       .014f*Mathf.Sin(u*Mathf.PI*2+j),-knotDrop-.24f*u,.011f*Mathf.Sin(u*Mathf.PI*3+j));
                    var q=at(t);var q2=at(next);var x=Vector3.right*.002f;
                    b.Quad(0,q-x,q+x,q2+x,q2-x);
                }
            }
            return b.Finish("Birthday_BalloonCurvedStrings");
        }
    }
}
