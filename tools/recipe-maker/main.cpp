// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Trilex. Offline copy/literal recipe producer; contains no game data.
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <vector>
#include <unordered_map>
#include <filesystem>
#include <stdexcept>
static void Check(bool b){if(!b)throw std::runtime_error("recipe bounds");}
static std::vector<uint8_t> Read(const wchar_t* p){auto n=std::filesystem::file_size(p);Check(n<=128*1024*1024);FILE* f=nullptr;Check(!_wfopen_s(&f,p,L"rb")&&f);std::vector<uint8_t> d(size_t(n),0);auto r=fread(d.data(),1,d.size(),f);fclose(f);Check(r==d.size());return d;}
static uint64_t Key(const uint8_t* p){uint64_t k;memcpy(&k,p,8);return k;}
int wmain(int argc,wchar_t** argv){try{
 Check(argc==4);auto original=Read(argv[1]),target=Read(argv[2]);
 std::unordered_map<uint64_t,uint32_t> index;index.max_load_factor(.85f);index.reserve(original.size()/4);
 for(size_t p=0;p+8<=original.size();p+=4)index.emplace(Key(original.data()+p),uint32_t(p));
 FILE* f=nullptr;Check(!_wfopen_s(&f,argv[3],L"wbx")&&f);
 auto put=[&](auto n){Check(fwrite(&n,sizeof(n),1,f)==1);};
 Check(fwrite("SVRP",1,4,f)==4);put(uint32_t(1));put(uint64_t(target.size()));put(uint32_t(0));uint32_t commands=0;size_t cursor=0,pending=0,literalBytes=0,copyBytes=0;
 auto literal=[&](size_t end){if(end==pending)return;put(uint8_t(1));put(uint32_t(end-pending));Check(fwrite(target.data()+pending,1,end-pending,f)==end-pending);literalBytes+=end-pending;++commands;};
 while(cursor+8<=target.size()){
  const auto hit=index.find(Key(target.data()+cursor));
  if(hit==index.end()){++cursor;continue;}
  const size_t source=hit->second;size_t length=8;
  while(cursor+length<target.size()&&source+length<original.size()&&target[cursor+length]==original[source+length])++length;
  literal(cursor);put(uint8_t(0));put(uint64_t(source));put(uint32_t(length));copyBytes+=length;++commands;cursor+=length;pending=cursor;
 }
 literal(target.size());Check(commands<=1000000);Check(!fseek(f,16,SEEK_SET));put(commands);Check(!fclose(f));
 printf("commands=%u originalBytes=%zu literalBytes=%zu\n",commands,copyBytes,literalBytes);return 0;
}catch(...){fputs("Recipe preparation refused\n",stderr);return 1;}}
