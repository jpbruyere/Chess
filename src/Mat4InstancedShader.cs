using System;
using OpenTK.Graphics.OpenGL;
using System.Diagnostics;
using OpenTK;
using Tetra;

using Tetra.DynamicShading;

namespace Chess
{
	public class Mat4InstancedShader : Tetra.Shader
	{
		public Mat4InstancedShader ():base(){}
		public int DiffuseTexture;

		public override void Init ()
		{
			vertSource = @"
			#version 330
			#extension GL_ARB_shader_subroutine : require

			precision mediump float;

			layout (location = 0) in vec3 in_position;
			layout (location = 1) in vec2 in_tex;
			layout (location = 2) in vec3 in_normal;
			layout (location = 4) in mat4 in_model;
			layout (location = 8) in vec4 in_color;

			layout (std140) uniform block_data{
				vec4 Color;
				mat4 ModelView;
				mat4 Projection;
				mat4 Normal;
				vec4 lightPos;
			};

			out vec2 texCoord;
			out vec3 n;			
			out vec4 vEyeSpacePos;
			out vec4 color;
			
			subroutine void vertexTech_t ();
			subroutine uniform vertexTech_t vertexTech;

			subroutine (vertexTech_t) void uninstancedPos() {
				gl_Position = Projection * ModelView * vec4(in_position.xyz, 1);
			}
			subroutine (vertexTech_t) void full() {
				texCoord = in_tex;
				n = vec3(Normal * in_model * vec4(in_normal, 0));

				vec3 pos = in_position.xyz;

				vEyeSpacePos = ModelView * in_model * vec4(pos, 1);
				color = in_color;
				
				gl_Position = Projection * vEyeSpacePos;
			}
			void main(void)
			{
				vertexTech();
			}";

			fragSource = @"
			#version 330
			#extension GL_ARB_shader_subroutine : require

			precision mediump float;

			uniform sampler2D tex;

			layout (std140) uniform block_data{
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

			const vec3 diffuse = vec3(1.0, 1.0, 1.0);
			const vec3 ambient = vec3(0.5, 0.5, 0.5);
			const vec3 specular = vec3(0.7,0.7,0.7);
			const float shininess =16.0;
			const float screenGamma = 1.0;

			subroutine vec4 computeColor_t ();
			subroutine uniform computeColor_t computeColor;

			subroutine (computeColor_t) vec4 simpleColor() {
				return Color;
			}
			subroutine (computeColor_t) vec4 blinnPhong(){
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

				diffTex.rgb = diffTex.rgb * (ambient + Idiff) + Ispec;
				return vec4(pow(diffTex.rgb, vec3(1.0/screenGamma)), diffTex.a);
			}
			subroutine (computeColor_t) vec4 textured(){
				vec4 diffTex = texture( tex, texCoord) * Color * color;
				if (diffTex.a == 0.0)
					discard;
				return diffTex;
			}
			void main(void)
			{
				out_frag_color = computeColor();
				gl_FragDepth = gl_FragCoord.z;
			}";
			base.Init ();
		}
		protected override void BindVertexAttributes ()
		{
			base.BindVertexAttributes ();

			GL.BindAttribLocation(pgmId, 2, "in_normal");
			GL.BindAttribLocation(pgmId, VertexArrayObject.instanceBufferIndex, "in_model");
		}
		int bi1;
		int computeColorSubroutine, simpleColorFunc, blinnPhongFunc, texturedFunc,
			vertexTeckSubroutine, fullFunc, uninstancedPosFunc;
		protected override void GetUniformLocations ()
		{
			simpleColorFunc = GL.GetSubroutineIndex (pgmId, ShaderType.FragmentShader, "simpleColor");
			blinnPhongFunc = GL.GetSubroutineIndex (pgmId, ShaderType.FragmentShader, "blinnPhong");
			texturedFunc = GL.GetSubroutineIndex (pgmId, ShaderType.FragmentShader, "textured");
			computeColorSubroutine = GL.GetSubroutineUniformLocation (pgmId, ShaderType.FragmentShader, "computeColor");

			fullFunc = GL.GetSubroutineIndex (pgmId, ShaderType.VertexShader, "full");
			uninstancedPosFunc = GL.GetSubroutineIndex (pgmId, ShaderType.VertexShader, "uninstancedPos");
			vertexTeckSubroutine = GL.GetSubroutineUniformLocation (pgmId, ShaderType.VertexShader, "vertexTech");

			bi1 = GL.GetUniformBlockIndex (pgmId, "block_data");
			GL.UniformBlockBinding(pgmId, bi1, 0);
		}	
		public override void Enable ()
		{
			GL.UseProgram (pgmId);
			GL.ActiveTexture (TextureUnit.Texture0);
			GL.BindTexture(TextureTarget.Texture2D, DiffuseTexture);
		}
		public void SetSimpleTexturedPass(){
			GL.UniformSubroutines (ShaderType.VertexShader, 1, ref fullFunc);
			GL.UniformSubroutines (ShaderType.FragmentShader, 1, ref texturedFunc);
		}
		public void SetSimpleColorPass(){
			GL.UniformSubroutines (ShaderType.VertexShader, 1, ref uninstancedPosFunc);
			GL.UniformSubroutines (ShaderType.FragmentShader, 1, ref simpleColorFunc);
		}
		public void SetLightingPass(){
			GL.UniformSubroutines (ShaderType.VertexShader, 1, ref fullFunc);
			GL.UniformSubroutines (ShaderType.FragmentShader, 1, ref blinnPhongFunc);
		}
	}
}

