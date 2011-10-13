﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vixen.Module.EffectEditor;
using Vixen.Commands;
using Vixen.Commands.KnownDataTypes;

namespace VixenModules.EffectEditor.LevelTypeEditor
{
	class LevelTypeEditorDescriptor : EffectEditorModuleDescriptorBase
	{
		private Guid _typeId = new Guid("{c3ad317b-3715-418c-9a79-2e315d235648}");
		private CommandParameterSignature _paramSpec = new CommandParameterSignature(
			new CommandParameterSpecification("Level", typeof(Level))
			);

		public override string Author { get { return "Vixen Team"; } }

		public override string Description { get { return "A control which will edit a parameter of type Level."; } }

		public override Guid EffectTypeId { get { return Guid.Empty; } }

		public override Type ModuleClass { get { return typeof(LevelTypeEditor); } }

		public override CommandParameterSignature ParameterSignature { get { return _paramSpec; } }

		public override Guid TypeId { get { return _typeId; } }

		public override string TypeName { get { return "Level Type Editor"; } }

		public override string Version { get { return "0.1"; } }
	}
}