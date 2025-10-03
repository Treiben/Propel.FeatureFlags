import React from 'react';
import PropelLogo from '../../propel.png';

interface PropelIconProps {
    className?: string;
    size?: number;
}

export const PropelIcon: React.FC<PropelIconProps> = ({ className = '', size = 32 }) => {
    return (
        <img
            src={PropelLogo}
            alt="Propel Feature Flags"
            width={size}
            height={size}
            className={`${className} object-contain`}
        />
    );
};