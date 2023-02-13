if table.getn(arg) < 1 then
    print("Usage: w <directory> [clone]")
    return
end

base = arg[1]
clone = arg[2] == "clone"

if clone then
    print("do you really want to overwrite the server files? [yes/NO]")
    if read() ~= "yes" then
        return
    end
end

ws, err = http.websocket("ws://<url>")
if err ~= nil then
    error(err)
end

fs.makeDir(base)
ws.send(tostring(os.getComputerID())) 


function recurse(list, path, relpath)
    if path == "/rom" then
        return
    end
        
    for _, entry in pairs(fs.list(path)) do
        if fs.isDir(path .. "/" .. entry) then
            recurse(list, path .. "/" .. entry, relpath .. "/" .. entry)
        else
            table.insert(list, relpath .. "/" .. entry)
        end
    end            
end
function get_file_manifest()
    files = {}
    path = base
    if path == "/" then
        path = ""
    end
    recurse(files, path, "")
    return files
end

if clone then
    print("sending clone request")
    message = "clone\n"
    manifest = get_file_manifest()
    for _, line in pairs(manifest) do
        message = message .. line .. "\n"
    end
    ws.send(message)
else
    ws.send("")
end

function head(text)
    split = string.find(text, "\n")
    first = string.sub(text, 1, split - 1)
    rest = string.sub(text, split + 1)
    return first, rest
end
    
function recurse(list, path, relpath)
    for _, entry in pairs(fs.list(path)) do
        if fs.isDir(path .. "/" .. entry) then
            recurse(list, path .. "/" .. entry, relpath .. "/" .. entry)
        else
            table.insert(list, relpath .. "/" .. entry)
        end
    end
end 

while true do
    msg = ws.receive()
    if msg == nil then
        print("server disconnected")
        return
    end
    
    op, rest = head(msg)
    if op == "create" then
        path = base .. "/" .. rest
        print("touch " .. path)
        dirPath = fs.getDir(path)
        fs.makeDir(dirPath)
        h = fs.open(path, "w")
        h.close()
    elseif op == "createdir" then
        path = base .. "/" .. rest
        print("mkdir " .. path)
        fs.makeDir(path)
    elseif op == "delete" then
        path = base .. "/" .. rest
        print("rm " .. path)
        fs.delete(path)
    elseif op == "write" then
        path, contents = head(rest)
        path = base .. "/" .. path
        length = string.len(contents)
        print("write " .. length .. " chars into " .. path)
        h = fs.open(path, "w")
        h.write(contents)
        h.close()
    elseif op == "move" then
        old_path, new_path = head(rest) 
        old_path = base .. "/" .. old_path
        new_path = base .. "/" .. new_path
        
        print("mv " .. old_path .. " " .. new_path)
        fs.move(old_path, new_path)      
    elseif op == "read" then
        path = base .. "/" .. rest
        print("read " .. path)
        h, err = fs.open(path, "r")    
        if h == nil then
            ws.send("err")
        else
            message = "ok\n"
            message = message .. h.readAll()
            h.close()
            ws.send(message)
        end
    elseif op == "ping" then
        ws.send("pong")
    end
end