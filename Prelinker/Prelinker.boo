import Mono.Cecil
import Mono.Cecil.Cil
import System.Collections.Generic

class CustomResolver(DefaultAssemblyResolver):
	def Add(assembly as AssemblyDefinition):
		RegisterAssembly(assembly)
		assembly.Resolver = self

class Prelinker:
	Assemblies = List [of AssemblyDefinition]()
	OutTypes = List [of TypeDefinition]()
	OutMethods = List [of MethodDefinition]()
	OutFields = List [of FieldDefinition]()
	OutProperties = List [of PropertyDefinition]()
	Resolver = CustomResolver()
	
	def constructor(args as (string)):
		if args.Length < 3:
			print 'Nothing to prelink'
			return
		
		inasm = LoadAssembly(args[1])
		for i in range(2, args.Length):
			LoadAssembly(args[i])
		AddMethod(inasm.EntryPoint)
		
		typeI = 0
		while typeI < inasm.MainModule.Types.Count:
			type = inasm.MainModule.Types[typeI]
			if not OutTypes.Contains(type):
				inasm.MainModule.Types.Remove(type)
				continue
			typeI += 1
			print type.Name
			methodI = 0
			while methodI < type.Methods.Count:
				method = type.Methods[methodI]
				if not OutMethods.Contains(method):
					type.Methods.Remove(method)
					continue
				methodI += 1
				print '\tmethod:', method.Name
			fieldI = 0
			while fieldI < type.Fields.Count:
				field = type.Fields[fieldI]
				if not OutFields.Contains(field):
					type.Fields.Remove(field)
					continue
				fieldI += 1
				print '\tfield:', field.Name
		
		AssemblyFactory.SaveAssembly(inasm, args[0])
		
		#while OutTypes.Count != 0:
		#	DefineType(outasm, OutTypes[0])
	
	def DefineType(outasm as AssemblyDefinition, outtype as TypeDefinition) as TypeDefinition:
		if not OutTypes.Contains(outtype):
			return
		OutTypes.Remove(outtype)
		print outtype.FullName
		
		baseType = null
		if outtype.BaseType != null:
			baseType = DefineType(outasm, outtype.BaseType.Resolve())
		#if outtype.HasInterfaces:
		#	for iface as TypeReference in outtype.Interfaces:
		#		DefineType(outasm, iface.Resolve())
		
		#type = outasm.MainModule.DefineType(outtype.Name, outtype.Namespace, outtype.Attributes)
		
		#return type
	
	def LoadAssembly(fn as string):
		asm = AssemblyFactory.GetAssembly(fn)
		Assemblies.Add(asm)
		Resolver.Add(asm)
		
		return asm
	
	def IsBuiltin(module as ModuleDefinition):
		modname = module.Name
		return modname.StartsWith('System.') or modname == 'CommonLanguageRuntimeLibrary'
	
	def AddType(typeref as TypeReference):
		return if IsBuiltin(typeref.Module)
		try:
			AddType(typeref.Resolve())
		except:
			pass
	def AddType(type as TypeDefinition):
		return if IsBuiltin(type.Module)
		if not OutTypes.Contains(type):
			#print 'Added type', type.FullName
			OutTypes.Add(type)
			if type.BaseType != null:
				AddType(type.BaseType)
			if type.HasInterfaces:
				for iface as TypeReference in type.Interfaces:
					AddType(iface)
	
	def AddMethod(methodref as MethodReference):
		return if IsBuiltin(methodref.DeclaringType.Module)
		try:
			AddMethod(methodref.Resolve())
		except:
			pass
	def AddMethod(method as MethodDefinition):
		return if IsBuiltin(method.DeclaringType.Module)
		if not OutMethods.Contains(method):
			#print 'Added method', method.Name
			OutMethods.Add(method)
			AddType(method.DeclaringType)
			WalkMethodBody(method.Body)
	
	def AddField(fieldref as FieldReference):
		return if IsBuiltin(fieldref.DeclaringType.Module)
		try:
			AddField(fieldref.Resolve())
		except:
			pass
	def AddField(field as FieldDefinition):
		return if IsBuiltin(field.DeclaringType.Module)
		if not OutFields.Contains(field):
			#print 'Added field', field.Name
			OutFields.Add(field)
			AddType(field.DeclaringType)
	
	TypeOpcodes = [
			OpCodes.Box, 
			OpCodes.Castclass, 
			OpCodes.Isinst, 
			OpCodes.Mkrefany, 
			OpCodes.Newarr, 
			OpCodes.Cpobj, 
			OpCodes.Initobj, 
			OpCodes.Ldelema, 
			OpCodes.Ldelem_Any, 
			OpCodes.Stelem_Any, 
			OpCodes.Ldobj, 
			OpCodes.Stobj, 
			OpCodes.Refanyval, 
			OpCodes.Sizeof, 
			OpCodes.Unbox, 
			OpCodes.Unbox_Any, 
		]
	MethodOpcodes = [
			OpCodes.Call, 
			OpCodes.Calli, 
			OpCodes.Callvirt, 
			OpCodes.Ldftn, 
			OpCodes.Ldvirtftn, 
			OpCodes.Newobj, 
		]
	FieldOpcodes = [
			OpCodes.Ldfld, 
			OpCodes.Ldflda, 
			OpCodes.Ldsfld, 
			OpCodes.Stfld, 
			OpCodes.Stsfld, 
		]
	AnyOpcodes = [
			OpCodes.Ldtoken, 
		]
	def WalkMethodBody(body as MethodBody):
		for inst as Instruction in body.Instructions:
			# Type references
			if TypeOpcodes.Contains(inst.OpCode):
				AddType(cast(TypeReference, inst.Operand))
			# Method references
			elif MethodOpcodes.Contains(inst.OpCode):
				AddMethod(cast(MethodReference, inst.Operand))
			# Field references
			elif FieldOpcodes.Contains(inst.OpCode):
				AddField(cast(FieldReference, inst.Operand))
			elif AnyOpcodes.Contains(inst.OpCode):
				if inst.Operand isa TypeReference:
					AddType(cast(TypeReference, inst.Operand))
				elif inst.Operand isa MethodReference:
					AddMethod(cast(MethodReference, inst.Operand))
				elif inst.Operand isa FieldReference:
					AddField(cast(FieldReference, inst.Operand))

Prelinker(argv)
