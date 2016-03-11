using System;
using OpenTK.Graphics.OpenGL;
using System.Diagnostics;
using OpenTK;
using Tetra;


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
			layout (location = 4) in mat4 in_model;

			layout (std140, index = 0) uniform block_data{
				vec4 Color;
				mat4 ModelView;
				mat4 Projection;
				mat4 Normal;
				vec4 lightPos;
			};

			out vec2 texCoord;			
			out vec3 n;			
			out vec4 vEyeSpacePos;
			

			void main(void)
			{				
				texCoord = in_tex;
				n = vec3(Normal * in_model * vec4(in_normal, 0));

				vec3 pos = in_position.xyz;

				vEyeSpacePos = ModelView * in_model * vec4(pos, 1);
				
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
			
			out vec4 out_frag_color;

			const vec3 diffuse = vec3(1.2, 1.2, 1.2);
			const vec3 ambient = vec3(0.01, 0.01, 0.01);
			const vec3 specular = vec3(1.0, 1.0, 1.0);
			const float shininess = 80.0;
			const float screenGamma = 1.0;

			void main(void)
			{

				vec4 diffTex = texture( tex, texCoord) * Color;
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

		public int DiffuseTexture;

		protected override void BindVertexAttributes ()
		{
			base.BindVertexAttributes ();

			GL.BindAttribLocation(pgmId, 2, "in_normal");
			GL.BindAttribLocation(pgmId, Tetra.IndexedVAO.instanceBufferIndex, "in_model");
		}
		protected override void GetUniformLocations ()
		{	
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

