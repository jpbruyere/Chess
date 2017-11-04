using System;
using Tetra.DynamicShading;
using OpenTK;

namespace Chess
{
	public class InstancedChessModel : InstancedModel<VAOChessData> {
		public InstancedChessModel(MeshPointer pointer) : base(pointer) {}

		public void SetModelMat (int index, Matrix4 modelMat){
			Instances.InstancedDatas[index].modelMats = modelMat;
			SyncVBO = true;
		}
		public void SetColor (int index, Vector4 color){
			Instances.InstancedDatas[index].color = color;
			SyncVBO = true;
		}
		public void SetAmbient (int index, Vector4 color){
			Instances.InstancedDatas[index].ambient = color;
			SyncVBO = true;
		}
		public void SetSpecular (int index, Vector4 color){
			Instances.InstancedDatas[index].specular = color;
			SyncVBO = true;
		}
		public void Set (int index, Matrix4 modelMat, Vector4 color){
			Instances.InstancedDatas[index].modelMats = modelMat;
			Instances.InstancedDatas[index].color = color;
			SyncVBO = true;
		}
		public void Set (Matrix4 modelMat, Vector4 color){
			if (Instances == null)
				Instances = new InstancesVBO<VAOChessData> (new VAOChessData[1]);
			Instances.InstancedDatas[0].modelMats = modelMat;
			Instances.InstancedDatas[0].color = color;
			SyncVBO = true;
		}
		public int AddInstance (){
			if (Instances == null)
				Instances = new InstancesVBO<VAOChessData> (new VAOChessData[1]);
			int idx = Instances.AddInstance ();
			SyncVBO = true;
			return idx;
		}
		public void AddInstance (Matrix4 modelMat, Vector4 color){
			if (Instances == null)
				Instances = new InstancesVBO<VAOChessData> (new VAOChessData[1]);
			Instances.AddInstance (new VAOChessData (modelMat, color));
			SyncVBO = true;
		}
		public void RemoveInstance (int index){
			Instances.RemoveInstance (index);
			SyncVBO = true;
		}

		public void UpdateBuffer(){
			if (!SyncVBO)
				return;
			Instances.UpdateVBO();
			SyncVBO = false;
		}
	}
}

