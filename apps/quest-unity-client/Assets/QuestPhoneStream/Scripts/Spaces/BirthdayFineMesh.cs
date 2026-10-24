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

        // B2.5/2.6: arch silhouettes and seams use shared submeshes, no colliders,
        // no per-petal GameObject. Panel triangles are double-sided in BirthdayDecor.
        static Mesh Arch(float halfWidth, float bottom, float shoulders, float archHeight,
            float depth, bool facesWest, string name)
        {
            const int parts=28;
            var b=new Builder(2); // textured matte panel + champagne rolled piping
            Func<float,float> top=z=>shoulders + archHeight *
                Mathf.Sqrt(Mathf.Max(0f,1f-z*z/(halfWidth*halfWidth)));
            Func<float,float,Vector3> pt=(z,y)=>facesWest?
                new Vector3(depth,y,z):new Vector3(z,y,depth);
            Func<float,float,Vector3> trim=(z,y)=>facesWest?
                new Vector3(depth-.015f,y,z):new Vector3(z,y,depth-.015f);
            for(int i=0;i<parts;i++)
            {
                float a=Mathf.Lerp(-halfWidth,halfWidth,(float)i/parts);
                float c=Mathf.Lerp(-halfWidth,halfWidth,(float)(i+1)/parts);
                b.Quad(0,pt(a,bottom),pt(c,bottom),pt(c,top(c)),pt(a,top(a)));
                // Top metallic piping follows the arch rather than floating above it.
                b.Quad(1,trim(a,top(a)-.013f),trim(c,top(c)-.013f),
                    trim(c,top(c)+.008f),trim(a,top(a)+.008f));
            }
            // Two thin metallic vertical inlays mark the panel edges.
            float z0=halfWidth-.012f;
            b.Quad(1,trim(-z0,bottom),trim(-z0+.008f,bottom),
                trim(-z0+.008f,shoulders),trim(-z0,shoulders));
            b.Quad(1,trim(z0-.008f,bottom),trim(z0,bottom),
                trim(z0,shoulders),trim(z0-.008f,shoulders));
            return b.Finish(name);
        }
        public static Mesh WelcomeArch()
        {
            return Arch(.39f,.025f,.73f,.26f,.055f,false,
                "Birthday_Welcome_ArchedFabricSign");
        }
        public static Mesh GalleryArch()
        {
            return Arch(.89f,.30f,2.16f,.31f,.055f,true,
                "Birthday_Gallery_ArchedBackdrop");
        }
        public static Mesh DiningTableRunner()
        {
            var b=new Builder(4);
            // Lightweight stitched textile runner ~0.82 x 0.46 m on the table.
            b.Box(0,new Vector3(0,.010f,0),new Vector3(.42f,.004f,.23f));
            // One shared cream platter, five macarons in grouped pastel hues.
            foreach(float x in new[]{-.34f,.34f})
            {
                b.Box(1,new Vector3(x,.022f,.14f),new Vector3(.080f,.006f,.067f));
                for(int i=0;i<3;i++)
                {
                    var center=new Vector3(x+(i-1)*.039f,.038f,.14f);
                    b.Box(i%2==0?2:3,center,new Vector3(.017f,.014f,.018f));
                    b.Box(1,center+Vector3.up*.015f,new Vector3(.017f,.002f,.018f));
                }
            }
            return b.Finish("Birthday_DiningRunner_DessertPlates");
        }


        // B2.9: asymmetrical organic garland, entire cluster is one 3-material mesh.
        // Per-side counts vary, keeping the photograph centers and XR ray interaction clear.
        public static Mesh GalleryOrganicGarland(bool left)
        {
            var b=new Builder(3);
            int count=left?19:13;
            for(int i=0;i<count;i++)
            {
                float t=(i+.5f)/count;
                float wave=Mathf.Sin(i*2.39996f);
                float z=(left?-1f:1f)*(.78f+.064f*Mathf.Sin(i*1.77f));
                float y=.61f+1.72f*t+(i%3-1)*.043f;
                float x=-.14f-.044f*Mathf.Cos(i*1.41f);
                float r=.065f+.018f*(i%4)+.011f*Mathf.Abs(wave);
                AddEllipsoid(b,i%3,new Vector3(x,y,z),new Vector3(r*.88f,r*1.21f,r),7,10);
            }
            return b.Finish(left?"Birthday_OrganicGarland_Left19":"Birthday_OrganicGarland_Right13");
        }
        static void AddEllipsoid(Builder b,int material,Vector3 c,Vector3 r,int latitude,int longitude)
        {
            for(int j=0;j<latitude;j++)
            {
                float p0=-Mathf.PI/2f+Mathf.PI*j/latitude;
                float p1=-Mathf.PI/2f+Mathf.PI*(j+1)/latitude;
                for(int k=0;k<longitude;k++)
                {
                    float a=2f*Mathf.PI*k/longitude, a1=2f*Mathf.PI*(k+1)/longitude;
                    Func<float,float,Vector3> v=(p,angle)=> c+new Vector3(
                        r.x*Mathf.Cos(p)*Mathf.Cos(angle),r.y*Mathf.Sin(p),
                        r.z*Mathf.Cos(p)*Mathf.Sin(angle));
                    b.Quad(material,v(p0,a),v(p0,a1),v(p1,a1),v(p1,a));
                }
            }
        }
        public static Mesh GalleryInnerLayer()
        {
            // Behind all photo quads at X=-.041; in front of existing outer panel
            // X=.055. This adds reveal and rolled piping without hiding photos.
            return Arch(.68f,.49f,1.96f,.18f,.015f,true,
                "Birthday_Gallery_InnerCreamReveal");
        }
        public static Mesh DiningCeramicsAndGlass()
        {
            var b=new Builder(3); // porcelain, champagne rim, frosted glass
            for(int i=0;i<2;i++)
            {
                float s=i==0?-1f:1f;
                Vector3 center=new Vector3(s*.48f,.03f,-.11f);
                // Small porcelain platter, gold rim, and sturdy frosted goblet.
                AddEllipsoid(b,0,center,new Vector3(.082f,.009f,.064f),3,12);
                AddEllipsoid(b,1,center+Vector3.up*.012f,new Vector3(.069f,.003f,.055f),3,12);
                var basePt=new Vector3(s*.51f,.055f,-.24f);
                b.Box(1,basePt,new Vector3(.029f,.004f,.029f));
                b.Box(2,basePt+Vector3.up*.037f,new Vector3(.005f,.039f,.005f));
                AddEllipsoid(b,2,basePt+Vector3.up*.093f,new Vector3(.044f,.045f,.044f),5,12);
                AddEllipsoid(b,1,basePt+Vector3.up*.134f,new Vector3(.043f,.004f,.043f),3,12);
            }
            return b.Finish("Birthday_Table_CeramicDessertAndFrostedGlasses");
        }
        public static Mesh CakePipedRosettes()
        {
            var b=new Builder(2); // cream swirls and champagne beading
            for(int i=0;i<18;i++)
            {
                float a=i*Mathf.PI*2f/18f;
                var center=new Vector3(Mathf.Cos(a)*.144f,.277f,Mathf.Sin(a)*.144f);
                AddEllipsoid(b,0,center,new Vector3(.020f,.012f,.020f),3,8);
                if(i%3==0)AddEllipsoid(b,1,center+Vector3.up*.014f,
                    new Vector3(.009f,.008f,.009f),3,7);
            }
            return b.Finish("Birthday_Cake_PipedCreamAndGoldAccents");
        }

        // B3.1: a small botanical emblem sits in the upper part of the existing
        // entry arch, above the greeting. It uses no photo assets or colliders.
        public static Mesh WelcomeBotanicalCrest()
        {
            var b = new Builder(3); // sage leaves, champagne line, ivory medallion
            b.Box(1, new Vector3(0,.827f,-.107f), new Vector3(.095f,.004f,.006f));
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 3; i++)
                {
                    float x = side * (.085f + i * .051f);
                    float y = .839f + (i % 2) * .029f;
                    AddEllipsoid(b, 0, new Vector3(x,y,-.113f),
                        new Vector3(.035f,.013f,.007f), 4, 7);
                }
            }
            b.Box(1, new Vector3(0,.838f,-.120f), new Vector3(.025f,.025f,.006f));
            b.Box(2, new Vector3(0,.838f,-.129f), new Vector3(.015f,.015f,.003f));
            return b.Finish("Birthday_Welcome_BotanicalCrest");
        }

        // A compact tabletop styling piece between the real sideboard's
        // flowers and gifts. Everything is grouped into just three submeshes.
        public static Mesh SideboardCandleTray()
        {
            var b = new Builder(3); // ivory ceramic, champagne tray, warm accents
            // All three wax cylinders rest on the ceramic insert. Earlier B3.1
            // used h/2 for their mesh height and spaced the first beyond the rim.
            b.Box(1, new Vector3(-.02f,.012f,0),new Vector3(.153f,.016f,.085f));
            b.Box(0, new Vector3(-.02f,.022f,0),new Vector3(.137f,.006f,.071f));
            for(int i=0;i<3;i++)
            {
                float x = -.069f+i*.049f;
                float h = i == 1 ? .092f : (i == 0 ? .068f : .079f);
                // Builder.Box accepts HALF-extents: wax bottom must equal
                // insert top (.025m), rather than intersecting the tabletop.
                b.Box(0,new Vector3(x,.025f+h*.5f,0),
                    new Vector3(.017f,h*.5f,.017f));
                b.Box(1,new Vector3(x,.025f+h+.0015f,0),
                    new Vector3(.018f,.003f,.018f));
                b.Box(2,new Vector3(x,.025f+h+.008f,0),
                    new Vector3(.007f,.011f,.007f));
            }
            return b.Finish("Birthday_Sideboard_ThreeCandles");
        }

        // Two slim decorative brass wall lamps frame the memory gallery.
        // They are purely emissive detail, NOT realtime Lights or shadow casters.
        // Stay outside all three photo-frame footprints (|z| <= 0.545m).
        public static Mesh GalleryBrassSconces()
        {
            var b = new Builder(3); // champagne, ivory glass, warm center
            foreach (float z in new [] { -.66f, .66f })
            {
                b.Box(0, new Vector3(-.105f,1.53f,z),
                    new Vector3(.010f,.172f,.031f));
                b.Box(1, new Vector3(-.128f,1.49f,z),
                    new Vector3(.015f,.107f,.025f));
                b.Box(2, new Vector3(-.146f,1.49f,z),
                    new Vector3(.003f,.068f,.010f));
                b.Box(0, new Vector3(-.130f,1.655f,z),
                    new Vector3(.022f,.008f,.041f));
                b.Box(0, new Vector3(-.130f,1.323f,z),
                    new Vector3(.022f,.008f,.041f));
            }
            return b.Finish("Birthday_Gallery_BrassWallSconces");
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
