using System;
using OpenTK.Graphics.OpenGL;
using System.Diagnostics;
using OpenTK;
using Tetra;
using System.Runtime.InteropServices;


namespace Chess
{
	public class Mat4InstancedShader : Shader
	{
		public Mat4InstancedShader ()
		{
			vertSource = @"
			#version 330
			precision highp float;

			layout (location = 0) in vec3 in_position;
			layout (location = 1) in vec2 in_tex;
			layout (location = 2) in vec3 in_normal;
			layout (location = 4) in vec4 in_weights;
			layout (location = 5) in mat4 in_model;
			layout (location = 9) in vec4 in_color;

			layout (std140, index = 0) uniform block_data{
				vec4 Color;
				mat4 ModelView;
				mat4 Projection;
				mat4 Normal;
				vec4 lightPos;
			};

			uniform mat4 bones[4];

			out vec2 texCoord;			
			out vec3 n;			
			out vec4 vEyeSpacePos;
			out vec4 color;
			

			void main(void)
			{				
				texCoord = in_tex;
				n = vec3(Normal * in_model * vec4(in_normal, 0));

				vec3 pos = in_position.xyz;

				vEyeSpacePos = ModelView * in_model * vec4(pos, 1);
				color = in_color;
				
				gl_Position = Projection * ModelView * in_model * vec4(pos, 1);
			}";

			fragSource = @"
			#version 330			

			precision highp float;

			uniform sampler2D tex;			

			layout (std140, index = 0) uniform block_data{
				vec4 Color;
				mat4 ModelView;
				mat4 Projection;
				mat4 Normal;
				vec4 lightPos;
			};

			in vec2 texCoord;			
			in vec4 vEyeSpacePos;
			in vec3 n;
			in vec4 color;
			
			out vec4 out_frag_color;

			const vec3 diffuse = vec3(1.4, 1.4, 1.4);
			const vec3 ambient = vec3(0.01, 0.01, 0.01);
			const vec3 specular = vec3(1.0, 1.0, 1.0);
			const float shininess = 30.0;
			const float screenGamma = 1.0;

			void main(void)
			{

				vec4 diffTex = texture( tex, texCoord) * Color * color;
				if (diffTex.a == 0.0)
					discard;

				vec3 vLight;
				vec3 vEye = normalize(-vEyeSpacePos.xyz);

				if (lightPos.w == 0.0)
					vLight = normalize(-lightPos.xyz);
				else
					vLight = normalize(lightPos.xyz - vEyeSpacePos.xyz);

				//blinn phong
				vec3 halfDir = normalize(vLight + vEye);
				float specAngle = max(dot(halfDir, n), 0.0);
				vec3 Ispec = specular * pow(specAngle, shininess);
				vec3 Idiff = diffuse * max(dot(n,vLight), 0.0);

				vec3 colorLinear = diffTex.rgb + diffTex.rgb * (ambient + Idiff) + Ispec;

				out_frag_color = vec4(pow(colorLinear, vec3(1.0/screenGamma)), diffTex.a);
			}";
			Compile ();
		}

		public int DiffuseTexture, bonesLocation;
		Matrix4[] bones;

		public Matrix4[] Bones {
			get { return bones; }
			set {
				bones = value;
				int m4Size = Marshal.SizeOf (typeof(Matrix4));
				float[] vBones = new float[bones.Length * m4Size];
				for (int i = 0; i < bones.Length; i++) {
					IntPtr ptr = Marshal.AllocHGlobal(m4Size);
					Marshal.StructureToPtr (bones [i], ptr, false);
					Marshal.Copy (ptr, vBones, i * m4Size, m4Size);
				}

				GL.UniformMatrix4 (bonesLocation, 4, false, vBones);
			}
		}

		protected override void BindVertexAttributes ()
		{
			base.BindVertexAttributes ();

			GL.BindAttribLocation(pgmId, 2, "in_normal");
			GL.BindAttribLocation(pgmId, Tetra.IndexedVAO.instanceBufferIndex, "in_model");
		}
		protected override void GetUniformLocations ()
		{
			bonesLocation = GL.GetUniformLocation(pgmId, "bones");
			GL.UniformBlockBinding(pgmId, GL.GetUniformBlockIndex(pgmId, "block_data"), 0);
		}	
		public override void Enable ()
		{
			GL.UseProgram (pgmId);
			GL.ActiveTexture (TextureUnit.Texture0);
			GL.BindTexture(TextureTarget.Texture2D, DiffuseTexture);
		}
	}
}

