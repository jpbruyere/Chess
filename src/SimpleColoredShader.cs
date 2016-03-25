using System;
using OpenTK.Graphics.OpenGL;
using System.Diagnostics;
using OpenTK;
using Tetra;

namespace Chess
{
	public class SimpleColoredShader : Tetra.Shader
	{
		public SimpleColoredShader ():base(){
		}
		public override void Init ()
		{
			vertSource = @"
			#version 330
			precision highp float;

			layout (location = 0) in vec3 in_position;

			layout (std140, index = 0) uniform block_data{
				vec4 Color;
				mat4 ModelView;
				mat4 Projection;
				mat4 Normal;
				vec4 lightPos;
			};
			
			void main(void)
			{								
				gl_Position = Projection * ModelView * vec4(in_position, 1);
			}";

			fragSource = @"
			#version 330			

			precision highp float;

			layout (std140, index = 0) uniform block_data{
				vec4 Color;
				mat4 ModelView;
				mat4 Projection;
				mat4 Normal;
				vec4 lightPos;
			};

			out vec4 out_frag_color;

			void main(void)
			{
				out_frag_color = Color;
			}";

			base.Init ();
		}
		protected override void BindVertexAttributes ()
		{
			GL.BindAttribLocation(pgmId, 0, "in_position");
		}
		protected override void GetUniformLocations ()
		{	
			GL.UniformBlockBinding(pgmId, GL.GetUniformBlockIndex(pgmId, "block_data"), 0);
		}	
		public override void Enable ()
		{
			GL.UseProgram (pgmId);
		}
	}
}

