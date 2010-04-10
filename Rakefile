require 'pp'

class Find
	def initialize(block)
		@include = []
		@exclude = []
		
		instance_eval &block
	end
	
	def include(files)
		files = [files] if not files.is_a? Array
		@include += files
	end
	
	def exclude(files)
		files = [files] if not files.is_a? Array
		@exclude += files
	end
	
	def find
		files = []
		@include.each do |path|
			if path =~ /\.\.\.\//
				(0...10).each do |i|
					files += Dir.glob(path.sub '.../', ('*/'*i))
				end
			elsif path =~ /\*/
				files += Dir.glob path
			else
				files += [path]
			end
		end
		
		files.map! do |file|
			if @exclude.map do |exclude|
						if file[0...exclude.size] == exclude then true
						else false
						end
					end.include? true
				nil
			else file
			end
		end
		files.compact
	end
end

def find(&block)
	Find.new(block).find
end

def cs(out, flags=[], files=[], &block)
	if block != nil
		files += find &block
	end
	deps = files
	files = files.map { |x| x.gsub '/', '\\' }
	
	target = 
		if out =~ /\.dll$/ then 'library'
		elsif out =~ /\.win\.exe$/ then 'winexe'
		else 'exe'
		end
	
	references = files.map do |file|
			if file =~ /\.dll$/ then file
			else nil
			end
		end.compact
	
	files = files.map do |file|
			if references.include? file then nil
			else file
			end
		end.compact
	
	references.map! { |file| "/reference:#{file}" }
	
	deps.reject! { |file| file.index('System.') == 0 }
	
	file out => deps do
		sh 'csc', "/out:#{out}", "/target:#{target}", *references, *flags, *files
	end
	Rake::Task[out].invoke
end

def boo(out, flags=[], files=[], &block)
	if block != nil
		files += find &block
	end
	deps = files
	
	target = 
		if out =~ /\.dll$/ then 'library'
		elsif out =~ /\.win\.exe$/ then 'winexe'
		else 'exe'
		end
	
	references = files.map do |file|
			if file =~ /\.dll$/ then file
			else nil
			end
		end.compact
	
	files = files.map do |file|
			if references.include? file then nil
			else file
			end
		end.compact
	
	references.map! { |file| "-reference:#{file}" }
	
	deps.reject! { |file| file.index('System.') == 0 }
	
	file out => deps do
		sh 'booc', "-o:#{out}", "-target:#{target}", *references, *flags, *files
	end
	Rake::Task[out].invoke
end

task :default => [:dotpack, :tests]

task :obj do
	Dir::mkdir 'Obj' if not FileTest::directory? 'Obj'
	Dir::mkdir 'Obj/Tests' if not FileTest::directory? 'Obj/Tests'
end

task :cecil => [:obj] do
	cs 'Obj/Mono.Cecil.dll', ['-keyfile:Mono.Cecil/mono.snk'] do
		include 'Mono.Cecil/.../*.cs'
	end
end

task :prelinker => [:obj, :cecil] do
	boo 'Obj/Prelinker.exe' do
		include 'Obj/Mono.Cecil.dll'
		include 'Prelinker/.../*.boo'
	end
end

task :dotpack => [:obj, :cecil, :prelinker] do
	cs 'Obj/LZMA.dll' do
		include '7zip/.../*.cs'
	end
	
	csflags = ['/o', '/nowin32manifest', '/win32res:Small.res', '/warn:4']
	cs 'Obj/Stage1.Deflate.exe', csflags do
		include 'Stages/Stage1.Executable.Deflate.cs'
	end
	cs 'Obj/Stage1.Deflate.Params.exe', csflags + ['/define:WITHARGS'] do
		include 'Stages/Stage1.Executable.Deflate.cs'
	end
	csflags = ['/o', '/nowin32manifest', '/win32res:Empty.res', '/warn:4']
	cs 'Obj/Stage2.Deflate.dll', csflags do
		include 'Stages/Stage2.Deflate.cs'
	end
	cs 'Obj/Stage2.LZMA.dll', csflags do
		include 'Stages/Stage2.LZMA.cs'
	end
	
	sldir = 'C:\Program Files (x86)\Microsoft Silverlight\4.0.50303.0'
	sllibs = %w{mscorlib system System.Windows}
	sllibs.map! {|x| '/r:' + sldir + '\\' + x + '.dll' }
	
	csflags += ['/nostdlib+', '/noconfig'] + sllibs
	cs 'Obj/Stage1.Silverlight.LZMA.dll', csflags do
		include 'Stages/Stage1.Silverlight.LZMA.cs'
	end
	
	boo 'Obj/DotPack.exe' do
		include 'Obj/LZMA.dll'
		include 'Obj/Mono.Cecil.dll'
		include 'Ionic.Zip.dll'
		include '.../*.boo'
		exclude 'Prelinker'
		exclude 'Tests'
		exclude 'Stages'
	end
end

task :tests => [:obj] do
	boo 'Obj/Tests/HelloWorld.exe' do
		include 'Tests/HelloWorld.boo'
	end
end
