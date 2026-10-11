print("Hello, World!")

--封装
Object = {}
--实例化方法
 function Object:new()
    local obj = {}
    self.__index = self
    setmetatable(obj, self)
    return obj
 end

 --继承
 function Object:subClass(className)
    _G[className] = {}
    local obj = _G[className]
    obj.base = self
    self.__index = self
    setmetatable(obj, self)
 end

 --声明一个新的类
Object:subClass("GameObject")
--成员变量
GameObject.posX=0
GameObject.posY=0
--成员方法
function GameObject:Move()
	self.posX = self.posX + 1
	self.posY = self.posY + 1
end

local obj = GameObject:new()
print(obj.posX)
obj:Move()
print(obj.posX)

local obj2 = GameObject:new()
print(obj2.posX)
obj2:Move()
print(obj2.posX)