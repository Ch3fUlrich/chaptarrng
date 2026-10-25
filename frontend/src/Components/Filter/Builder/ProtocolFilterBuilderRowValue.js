import React from 'react';
import FilterBuilderRowValue from './FilterBuilderRowValue';

const protocols = [
  { id: 'direct', name: 'Direct' },
  { id: 'soulseek', name: 'Soulseek' },
  { id: 'torrent', name: 'Torrent' },
  { id: 'usenet', name: 'Usenet' }
];

function ProtocolFilterBuilderRowValue(props) {
  return (
    <FilterBuilderRowValue
      tagList={protocols}
      {...props}
    />
  );
}

export default ProtocolFilterBuilderRowValue;
